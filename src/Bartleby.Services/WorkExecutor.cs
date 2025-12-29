using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Bartleby.Services.Prompts;
using Microsoft.Extensions.Logging;

namespace Bartleby.Services;

/// <summary>
/// Orchestrates AI work execution with structured prompts and response handling.
/// </summary>
/// <remarks>
/// <para>
/// The WorkExecutor is responsible for:
/// <list type="bullet">
/// <item>Building rich context for AI execution</item>
/// <item>Selecting and applying prompt templates based on transformation type</item>
/// <item>Parsing AI responses and handling different outcomes</item>
/// <item>Recording work sessions with provenance</item>
/// <item>Creating blocked questions when AI needs input</item>
/// </list>
/// </para>
/// </remarks>
public class WorkExecutor : IWorkExecutor
{
    private readonly IAIProvider _aiProvider;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IWorkSessionRepository _workSessionRepository;
    private readonly IBlockedQuestionRepository _blockedQuestionRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<WorkExecutor> _logger;

    public WorkExecutor(
        IAIProvider aiProvider,
        IWorkItemRepository workItemRepository,
        IWorkSessionRepository workSessionRepository,
        IBlockedQuestionRepository blockedQuestionRepository,
        ISettingsRepository settingsRepository,
        ILogger<WorkExecutor> logger)
    {
        _aiProvider = aiProvider ?? throw new ArgumentNullException(nameof(aiProvider));
        _workItemRepository = workItemRepository ?? throw new ArgumentNullException(nameof(workItemRepository));
        _workSessionRepository = workSessionRepository ?? throw new ArgumentNullException(nameof(workSessionRepository));
        _blockedQuestionRepository = blockedQuestionRepository ?? throw new ArgumentNullException(nameof(blockedQuestionRepository));
        _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WorkExecutionResponse> ExecuteAsync(
        WorkExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation(
            "Executing {TransformationType} on work item {WorkItemId}: {Title}",
            context.TransformationType,
            context.WorkItem.Id,
            context.WorkItem.Title);

        // Create a work session to track this execution
        var session = new WorkSession
        {
            WorkItemId = context.WorkItem.Id,
            TransformationType = context.TransformationType,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Build prompts using the template provider
            var systemPrompt = PromptTemplateProvider.GetSystemPrompt(
                context.TransformationType,
                context.WorkingDirectory);
            var userPrompt = PromptTemplateProvider.BuildUserPrompt(context);

            // Execute via AI provider
            var result = await _aiProvider.ExecutePromptAsync(
                systemPrompt,
                userPrompt,
                context.WorkingDirectory,
                cancellationToken);

            // Update and save session
            session.EndedAt = DateTime.UtcNow;
            session.TokensUsed = result.TokensUsed;
            session.Summary = result.Summary;
            session.ModifiedFiles = result.ModifiedFiles.ToList();
            session.Outcome = MapOutcome(result.Outcome);

            if (!result.Success)
            {
                session.ErrorMessage = result.ErrorMessage;
            }

            await _workSessionRepository.CreateAsync(session, cancellationToken);

            // Handle blocked questions
            if (result.Outcome == WorkExecutionOutcome.Blocked && result.Questions.Count > 0)
            {
                await CreateBlockedQuestionsAsync(
                    context.WorkItem.Id,
                    result.Questions,
                    result.Summary,
                    cancellationToken);
            }

            // Update work item attempt tracking
            await UpdateWorkItemAttemptAsync(context.WorkItem, cancellationToken);

            _logger.LogInformation(
                "Completed {TransformationType} on work item {WorkItemId} with outcome {Outcome}",
                context.TransformationType,
                context.WorkItem.Id,
                result.Outcome);

            return new WorkExecutionResponse
            {
                Success = result.Success,
                Outcome = result.Outcome,
                TransformationType = context.TransformationType,
                Summary = result.Summary,
                ModifiedFiles = result.ModifiedFiles,
                Questions = result.Questions,
                ErrorMessage = result.ErrorMessage,
                TokensUsed = result.TokensUsed,
                WorkSession = session
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing {TransformationType} on work item {WorkItemId}",
                context.TransformationType, context.WorkItem.Id);

            session.EndedAt = DateTime.UtcNow;
            session.Outcome = WorkSessionOutcome.Failed;
            session.ErrorMessage = ex.Message;

            await _workSessionRepository.CreateAsync(session, cancellationToken);

            return new WorkExecutionResponse
            {
                Success = false,
                Outcome = WorkExecutionOutcome.Failed,
                TransformationType = context.TransformationType,
                ErrorMessage = ex.Message,
                WorkSession = session
            };
        }
    }

    public async Task<WorkExecutionContext?> BuildContextAsync(
        Guid workItemId,
        TransformationType transformationType,
        CancellationToken cancellationToken = default)
    {
        var workItem = await _workItemRepository.GetByIdAsync(workItemId, cancellationToken);
        if (workItem == null)
        {
            _logger.LogWarning("Work item {WorkItemId} not found", workItemId);
            return null;
        }

        var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);
        var previousSessions = await _workSessionRepository.GetByWorkItemIdAsync(workItemId, cancellationToken);
        var questions = await _blockedQuestionRepository.GetByWorkItemIdAsync(workItemId, cancellationToken);
        var answeredQuestions = questions.Where(q => q.IsAnswered).ToList();

        return new WorkExecutionContext
        {
            WorkItem = workItem,
            TransformationType = transformationType,
            WorkingDirectory = settings.WorkingDirectory ?? Environment.CurrentDirectory,
            PreviousSessions = previousSessions.OrderBy(s => s.StartedAt).ToList(),
            AnsweredQuestions = answeredQuestions
        };
    }

    public async Task<TransformationType> GetNextTransformationAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _workSessionRepository.GetByWorkItemIdAsync(workItemId, cancellationToken);
        var orderedSessions = sessions.OrderBy(s => s.StartedAt).ToList();

        // If no sessions, start with Interpret
        if (orderedSessions.Count == 0)
        {
            return TransformationType.Interpret;
        }

        var lastSession = orderedSessions.Last();

        // Check for unanswered questions - if any, suggest AskClarification
        var questions = await _blockedQuestionRepository.GetByWorkItemIdAsync(workItemId, cancellationToken);
        if (questions.Any(q => !q.IsAnswered))
        {
            return TransformationType.AskClarification;
        }

        // Determine next based on last session outcome
        return lastSession.Outcome switch
        {
            WorkSessionOutcome.Completed => DetermineNextAfterSuccess(orderedSessions),
            WorkSessionOutcome.Blocked => TransformationType.AskClarification,
            WorkSessionOutcome.Failed => TransformationType.Refine,
            WorkSessionOutcome.InProgress => lastSession.TransformationType ?? TransformationType.Execute,
            _ => DetermineNextFromHistory(orderedSessions)
        };
    }

    private static TransformationType DetermineNextAfterSuccess(List<WorkSession> sessions)
    {
        // Find the last completed transformation type
        var lastTransformation = sessions
            .Where(s => s.Outcome == WorkSessionOutcome.Completed && s.TransformationType.HasValue)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault()?.TransformationType;

        return lastTransformation switch
        {
            TransformationType.Interpret => TransformationType.Plan,
            TransformationType.Plan => TransformationType.Execute,
            TransformationType.Execute => TransformationType.Finalize,
            TransformationType.Refine => TransformationType.Finalize,
            TransformationType.AskClarification => TransformationType.Execute,
            TransformationType.Finalize => TransformationType.Finalize, // Already complete
            _ => TransformationType.Plan
        };
    }

    private static TransformationType DetermineNextFromHistory(List<WorkSession> sessions)
    {
        // Count successful transformations by type
        var completedTypes = sessions
            .Where(s => s.Outcome == WorkSessionOutcome.Completed && s.TransformationType.HasValue)
            .Select(s => s.TransformationType!.Value)
            .ToHashSet();

        // Progress through the transformation pipeline
        if (!completedTypes.Contains(TransformationType.Interpret))
            return TransformationType.Interpret;
        if (!completedTypes.Contains(TransformationType.Plan))
            return TransformationType.Plan;
        if (!completedTypes.Contains(TransformationType.Execute))
            return TransformationType.Execute;

        return TransformationType.Finalize;
    }

    private async Task CreateBlockedQuestionsAsync(
        Guid workItemId,
        IEnumerable<string> questions,
        string? context,
        CancellationToken cancellationToken)
    {
        foreach (var questionText in questions)
        {
            var blockedQuestion = new BlockedQuestion
            {
                WorkItemId = workItemId,
                Question = questionText,
                Context = context,
                CreatedAt = DateTime.UtcNow
            };

            await _blockedQuestionRepository.CreateAsync(blockedQuestion, cancellationToken);

            _logger.LogDebug(
                "Created blocked question for work item {WorkItemId}: {Question}",
                workItemId,
                questionText);
        }
    }

    private async Task UpdateWorkItemAttemptAsync(WorkItem workItem, CancellationToken cancellationToken)
    {
        workItem.LastWorkedAt = DateTime.UtcNow;
        workItem.AttemptCount++;
        workItem.UpdatedAt = DateTime.UtcNow;

        await _workItemRepository.UpdateAsync(workItem, cancellationToken);
    }

    private static WorkSessionOutcome MapOutcome(WorkExecutionOutcome outcome)
    {
        return outcome switch
        {
            WorkExecutionOutcome.Completed => WorkSessionOutcome.Completed,
            WorkExecutionOutcome.Blocked => WorkSessionOutcome.Blocked,
            WorkExecutionOutcome.Failed => WorkSessionOutcome.Failed,
            WorkExecutionOutcome.NeedsMoreContext => WorkSessionOutcome.Blocked,
            _ => WorkSessionOutcome.Failed
        };
    }
}

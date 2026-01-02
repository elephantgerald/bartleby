using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Bartleby.Services;
using Bartleby.Services.Prompts;
using Microsoft.Extensions.Logging;
using Moq;

namespace Bartleby.Services.Tests;

/// <summary>
/// Integration tests for the complete blocked work flow:
/// AI blocks → questions stored → user answers → work resumes with context
/// </summary>
public class BlockedWorkFlowTests
{
    private readonly Mock<IAIProvider> _aiProviderMock;
    private readonly Mock<IWorkItemRepository> _workItemRepoMock;
    private readonly Mock<IWorkSessionRepository> _sessionRepoMock;
    private readonly Mock<IBlockedQuestionRepository> _questionRepoMock;
    private readonly Mock<ISettingsRepository> _settingsRepoMock;
    private readonly Mock<ILogger<WorkExecutor>> _loggerMock;
    private readonly PromptTemplateProvider _promptTemplateProvider;
    private readonly WorkExecutor _workExecutor;

    private readonly List<BlockedQuestion> _storedQuestions = [];
    private readonly List<WorkSession> _storedSessions = [];

    public BlockedWorkFlowTests()
    {
        _aiProviderMock = new Mock<IAIProvider>();
        _workItemRepoMock = new Mock<IWorkItemRepository>();
        _sessionRepoMock = new Mock<IWorkSessionRepository>();
        _questionRepoMock = new Mock<IBlockedQuestionRepository>();
        _settingsRepoMock = new Mock<ISettingsRepository>();
        _loggerMock = new Mock<ILogger<WorkExecutor>>();
        _promptTemplateProvider = new PromptTemplateProvider();

        // Settings
        _settingsRepoMock
            .Setup(r => r.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppSettings { WorkingDirectory = "/test/work" });

        // Session storage
        _sessionRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<WorkSession>(), It.IsAny<CancellationToken>()))
            .Callback<WorkSession, CancellationToken>((s, _) => _storedSessions.Add(s))
            .ReturnsAsync((WorkSession s, CancellationToken _) => s);

        _sessionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _storedSessions.Where(s => s.WorkItemId == id));

        // Question storage
        _questionRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<BlockedQuestion>(), It.IsAny<CancellationToken>()))
            .Callback<BlockedQuestion, CancellationToken>((q, _) => _storedQuestions.Add(q))
            .ReturnsAsync((BlockedQuestion q, CancellationToken _) => q);

        _questionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _storedQuestions.Where(q => q.WorkItemId == id));

        // Work item storage
        _workItemRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem w, CancellationToken _) => w);

        _workExecutor = new WorkExecutor(
            _aiProviderMock.Object,
            _workItemRepoMock.Object,
            _sessionRepoMock.Object,
            _questionRepoMock.Object,
            _settingsRepoMock.Object,
            _promptTemplateProvider,
            _loggerMock.Object);
    }

    [Fact]
    public async Task FullBlockedFlow_AIBlocksWithQuestions_QuestionsAreStored()
    {
        // Arrange
        var workItem = CreateWorkItem();
        var blockedResult = new WorkExecutionResult
        {
            Success = false,
            Outcome = WorkExecutionOutcome.Blocked,
            Summary = "Need more information to proceed",
            Questions = ["What database should be used?", "What authentication method?"],
            TokensUsed = 500
        };

        SetupWorkItemLookup(workItem);
        SetupAIResponse(blockedResult);

        var context = await _workExecutor.BuildContextAsync(
            workItem.Id,
            TransformationType.Execute);

        // Act
        var response = await _workExecutor.ExecuteAsync(context!);

        // Assert
        Assert.Equal(WorkExecutionOutcome.Blocked, response.Outcome);
        Assert.Equal(2, _storedQuestions.Count);
        Assert.Contains(_storedQuestions, q => q.Question == "What database should be used?");
        Assert.Contains(_storedQuestions, q => q.Question == "What authentication method?");
        Assert.True(_storedQuestions.All(q => q.WorkItemId == workItem.Id));
    }

    [Fact]
    public async Task FullBlockedFlow_AfterQuestionsAnswered_NextExecutionIncludesAnswers()
    {
        // Arrange
        var workItem = CreateWorkItem();

        // First execution - AI blocks
        var blockedResult = new WorkExecutionResult
        {
            Success = false,
            Outcome = WorkExecutionOutcome.Blocked,
            Summary = "Need more info",
            Questions = ["What API version?"],
            TokensUsed = 300
        };

        SetupWorkItemLookup(workItem);
        SetupAIResponse(blockedResult);

        var context1 = await _workExecutor.BuildContextAsync(
            workItem.Id,
            TransformationType.Execute);
        await _workExecutor.ExecuteAsync(context1!);

        // Simulate user answering the question
        var question = _storedQuestions.First();
        question.Answer = "Use REST API v2";
        question.AnsweredAt = DateTime.UtcNow;

        // Second execution - should include answered question
        var completedResult = new WorkExecutionResult
        {
            Success = true,
            Outcome = WorkExecutionOutcome.Completed,
            Summary = "Work completed",
            TokensUsed = 200
        };

        string? capturedUserPrompt = null;
        _aiProviderMock
            .Setup(p => p.ExecutePromptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, userPrompt, _, _) =>
                capturedUserPrompt = userPrompt)
            .ReturnsAsync(completedResult);

        // Act
        var context2 = await _workExecutor.BuildContextAsync(
            workItem.Id,
            TransformationType.Execute);
        await _workExecutor.ExecuteAsync(context2!);

        // Assert
        Assert.NotNull(capturedUserPrompt);
        Assert.Contains("Answered Questions", capturedUserPrompt);
        Assert.Contains("What API version?", capturedUserPrompt);
        Assert.Contains("Use REST API v2", capturedUserPrompt);
    }

    [Fact]
    public async Task GetNextTransformationAsync_WithUnansweredQuestions_SuggestsAskClarification()
    {
        // Arrange
        var workItem = CreateWorkItem();

        // Add a session and an unanswered question
        _storedSessions.Add(new WorkSession
        {
            WorkItemId = workItem.Id,
            Outcome = WorkSessionOutcome.Blocked,
            TransformationType = TransformationType.Execute,
            StartedAt = DateTime.UtcNow
        });

        _storedQuestions.Add(new BlockedQuestion
        {
            WorkItemId = workItem.Id,
            Question = "Unanswered question",
            Answer = null
        });

        // Act
        var nextTransformation = await _workExecutor.GetNextTransformationAsync(workItem.Id);

        // Assert
        Assert.Equal(TransformationType.AskClarification, nextTransformation);
    }

    [Fact]
    public async Task GetNextTransformationAsync_WithAllQuestionsAnswered_ContinuesNormalFlow()
    {
        // Arrange
        var workItem = CreateWorkItem();

        // Add a completed session and an answered question
        _storedSessions.Add(new WorkSession
        {
            WorkItemId = workItem.Id,
            Outcome = WorkSessionOutcome.Completed,
            TransformationType = TransformationType.Interpret,
            StartedAt = DateTime.UtcNow
        });

        _storedQuestions.Add(new BlockedQuestion
        {
            WorkItemId = workItem.Id,
            Question = "Answered question",
            Answer = "The answer",
            AnsweredAt = DateTime.UtcNow
        });

        // Act
        var nextTransformation = await _workExecutor.GetNextTransformationAsync(workItem.Id);

        // Assert
        Assert.Equal(TransformationType.Plan, nextTransformation);
    }

    [Fact]
    public async Task PromptTemplateProvider_WithAnsweredQuestions_IncludesQASection()
    {
        // Arrange
        var workItem = CreateWorkItem();
        var context = new WorkExecutionContext
        {
            WorkItem = workItem,
            TransformationType = TransformationType.Execute,
            WorkingDirectory = "/test",
            PreviousSessions = [],
            AnsweredQuestions =
            [
                new BlockedQuestion
                {
                    WorkItemId = workItem.Id,
                    Question = "What framework?",
                    Answer = ".NET 10",
                    AnsweredAt = DateTime.UtcNow
                },
                new BlockedQuestion
                {
                    WorkItemId = workItem.Id,
                    Question = "What database?",
                    Answer = "PostgreSQL",
                    AnsweredAt = DateTime.UtcNow
                }
            ]
        };

        // Act
        var prompt = _promptTemplateProvider.BuildUserPrompt(context);

        // Assert
        Assert.Contains("## Answered Questions", prompt);
        Assert.Contains("Q: What framework?", prompt);
        Assert.Contains("A: .NET 10", prompt);
        Assert.Contains("Q: What database?", prompt);
        Assert.Contains("A: PostgreSQL", prompt);
    }

    #region Helpers

    private WorkItem CreateWorkItem() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Test Work Item",
        Description = "Test description",
        Status = WorkItemStatus.InProgress
    };

    private void SetupWorkItemLookup(WorkItem workItem)
    {
        _workItemRepoMock
            .Setup(r => r.GetByIdAsync(workItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);
    }

    private void SetupAIResponse(WorkExecutionResult result)
    {
        _aiProviderMock
            .Setup(p => p.ExecutePromptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    #endregion
}

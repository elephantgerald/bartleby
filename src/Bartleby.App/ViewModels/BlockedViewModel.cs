using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Microsoft.Extensions.Logging;

namespace Bartleby.App.ViewModels;

public partial class BlockedViewModel : ObservableObject
{
    private readonly IBlockedQuestionRepository _questionRepository;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly ILogger<BlockedViewModel>? _logger;

    [ObservableProperty]
    public partial ObservableCollection<BlockedQuestionDisplay> Questions { get; set; }

    [ObservableProperty]
    public partial BlockedQuestionDisplay? SelectedQuestion { get; set; }

    [ObservableProperty]
    public partial string AnswerText { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public BlockedViewModel(
        IBlockedQuestionRepository questionRepository,
        IWorkItemRepository workItemRepository,
        ILogger<BlockedViewModel>? logger = null)
    {
        _questionRepository = questionRepository;
        _workItemRepository = workItemRepository;
        _logger = logger;
        Questions = [];
        AnswerText = string.Empty;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task LoadQuestionsAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var unanswered = await _questionRepository.GetUnansweredAsync(cancellationToken);
            Questions.Clear();

            foreach (var q in unanswered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var workItem = await _workItemRepository.GetByIdAsync(q.WorkItemId, cancellationToken);
                Questions.Add(new BlockedQuestionDisplay
                {
                    Question = q,
                    WorkItemTitle = workItem?.Title ?? "Unknown"
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load blocked questions");
            ErrorMessage = "Failed to load questions. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SubmitAnswerAsync(CancellationToken cancellationToken)
    {
        if (SelectedQuestion is null || string.IsNullOrWhiteSpace(AnswerText))
            return;

        ErrorMessage = null;

        try
        {
            var question = SelectedQuestion.Question;
            question.Answer = AnswerText;
            question.AnsweredAt = DateTime.UtcNow;

            await _questionRepository.UpdateAsync(question, cancellationToken);

            // Check if all questions for this work item are answered
            var remaining = await _questionRepository.GetByWorkItemIdAsync(question.WorkItemId, cancellationToken);
            if (remaining.All(q => q.IsAnswered))
            {
                var workItem = await _workItemRepository.GetByIdAsync(question.WorkItemId, cancellationToken);
                if (workItem is not null)
                {
                    // Restore previous status, or default to Ready if unknown
                    workItem.Status = workItem.PreviousStatus ?? WorkItemStatus.Ready;
                    workItem.PreviousStatus = null; // Clear the saved state
                    workItem.UpdatedAt = DateTime.UtcNow;
                    await _workItemRepository.UpdateAsync(workItem, cancellationToken);
                }
            }

            Questions.Remove(SelectedQuestion);
            SelectedQuestion = null;
            AnswerText = string.Empty;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to submit answer");
            ErrorMessage = "Failed to submit answer. Please try again.";
        }
    }
}

public class BlockedQuestionDisplay
{
    public required BlockedQuestion Question { get; set; }
    public required string WorkItemTitle { get; set; }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;

namespace Bartleby.App.ViewModels;

public partial class BlockedViewModel : ObservableObject
{
    private readonly IBlockedQuestionRepository _questionRepository;
    private readonly IWorkItemRepository _workItemRepository;

    [ObservableProperty]
    private ObservableCollection<BlockedQuestionDisplay> _questions = [];

    [ObservableProperty]
    private BlockedQuestionDisplay? _selectedQuestion;

    [ObservableProperty]
    private string _answerText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public BlockedViewModel(
        IBlockedQuestionRepository questionRepository,
        IWorkItemRepository workItemRepository)
    {
        _questionRepository = questionRepository;
        _workItemRepository = workItemRepository;
    }

    [RelayCommand]
    private async Task LoadQuestionsAsync()
    {
        IsLoading = true;

        try
        {
            var unanswered = await _questionRepository.GetUnansweredAsync();
            Questions.Clear();

            foreach (var q in unanswered)
            {
                var workItem = await _workItemRepository.GetByIdAsync(q.WorkItemId);
                Questions.Add(new BlockedQuestionDisplay
                {
                    Question = q,
                    WorkItemTitle = workItem?.Title ?? "Unknown"
                });
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SubmitAnswerAsync()
    {
        if (SelectedQuestion is null || string.IsNullOrWhiteSpace(AnswerText))
            return;

        var question = SelectedQuestion.Question;
        question.Answer = AnswerText;
        question.AnsweredAt = DateTime.UtcNow;

        await _questionRepository.UpdateAsync(question);

        // Check if all questions for this work item are answered
        var remaining = await _questionRepository.GetByWorkItemIdAsync(question.WorkItemId);
        if (remaining.All(q => q.IsAnswered))
        {
            var workItem = await _workItemRepository.GetByIdAsync(question.WorkItemId);
            if (workItem is not null)
            {
                workItem.Status = WorkItemStatus.Ready;
                await _workItemRepository.UpdateAsync(workItem);
            }
        }

        Questions.Remove(SelectedQuestion);
        SelectedQuestion = null;
        AnswerText = string.Empty;
    }
}

public class BlockedQuestionDisplay
{
    public required BlockedQuestion Question { get; set; }
    public required string WorkItemTitle { get; set; }
}

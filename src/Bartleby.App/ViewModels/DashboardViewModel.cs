using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;

namespace Bartleby.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IWorkItemRepository _workItemRepository;
    private readonly ISettingsRepository _settingsRepository;

    [ObservableProperty]
    public partial int TotalWorkItems { get; set; }

    [ObservableProperty]
    public partial int ReadyItems { get; set; }

    [ObservableProperty]
    public partial int InProgressItems { get; set; }

    [ObservableProperty]
    public partial int BlockedItems { get; set; }

    /// <summary>
    /// Indicates whether there are blocked items requiring attention.
    /// </summary>
    public bool HasBlockedItems => BlockedItems > 0;

    partial void OnBlockedItemsChanged(int value)
        => OnPropertyChanged(nameof(HasBlockedItems));

    [ObservableProperty]
    public partial int CompletedItems { get; set; }

    [ObservableProperty]
    public partial bool OrchestratorEnabled { get; set; }

    [ObservableProperty]
    public partial string OrchestratorStatus { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public DashboardViewModel(
        IWorkItemRepository workItemRepository,
        ISettingsRepository settingsRepository)
    {
        _workItemRepository = workItemRepository;
        _settingsRepository = settingsRepository;
        OrchestratorStatus = "Stopped";
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsLoading = true;

        try
        {
            var allItems = (await _workItemRepository.GetAllAsync()).ToList();
            TotalWorkItems = allItems.Count;
            ReadyItems = allItems.Count(x => x.Status == WorkItemStatus.Ready);
            InProgressItems = allItems.Count(x => x.Status == WorkItemStatus.InProgress);
            BlockedItems = allItems.Count(x => x.Status == WorkItemStatus.Blocked);
            CompletedItems = allItems.Count(x => x.Status == WorkItemStatus.Complete);

            var settings = await _settingsRepository.GetSettingsAsync();
            OrchestratorEnabled = settings.OrchestratorEnabled;
            OrchestratorStatus = settings.OrchestratorEnabled ? "Running" : "Stopped";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ToggleOrchestratorAsync()
    {
        var settings = await _settingsRepository.GetSettingsAsync();
        settings.OrchestratorEnabled = !settings.OrchestratorEnabled;
        await _settingsRepository.SaveSettingsAsync(settings);

        OrchestratorEnabled = settings.OrchestratorEnabled;
        OrchestratorStatus = settings.OrchestratorEnabled ? "Running" : "Stopped";
    }
}

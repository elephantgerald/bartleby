using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;

namespace Bartleby.App.ViewModels;

public partial class WorkItemsViewModel : ObservableObject
{
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IWorkSource _workSource;

    [ObservableProperty]
    private ObservableCollection<WorkItem> _workItems = [];

    [ObservableProperty]
    private WorkItem? _selectedWorkItem;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _filterStatus = "All";

    public WorkItemsViewModel(
        IWorkItemRepository workItemRepository,
        IWorkSource workSource)
    {
        _workItemRepository = workItemRepository;
        _workSource = workSource;
    }

    [RelayCommand]
    private async Task LoadWorkItemsAsync()
    {
        IsLoading = true;

        try
        {
            var items = await _workItemRepository.GetAllAsync();
            WorkItems.Clear();
            foreach (var item in items.OrderByDescending(x => x.UpdatedAt))
            {
                WorkItems.Add(item);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SyncFromSourceAsync()
    {
        IsLoading = true;

        try
        {
            var externalItems = await _workSource.SyncAsync();

            foreach (var item in externalItems)
            {
                var existing = await _workItemRepository.GetByExternalIdAsync(item.Source!, item.ExternalId!);
                if (existing is null)
                {
                    await _workItemRepository.CreateAsync(item);
                }
                else
                {
                    existing.Title = item.Title;
                    existing.Description = item.Description;
                    existing.Labels = item.Labels;
                    await _workItemRepository.UpdateAsync(existing);
                }
            }

            await LoadWorkItemsAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteWorkItemAsync(WorkItem? workItem)
    {
        if (workItem is null) return;

        await _workItemRepository.DeleteAsync(workItem.Id);
        WorkItems.Remove(workItem);
    }
}

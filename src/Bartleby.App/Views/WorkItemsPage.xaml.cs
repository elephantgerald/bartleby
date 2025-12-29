using Bartleby.App.ViewModels;

namespace Bartleby.App.Views;

public partial class WorkItemsPage : ContentPage
{
    public WorkItemsPage(WorkItemsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is WorkItemsViewModel vm)
        {
            await vm.LoadWorkItemsCommand.ExecuteAsync(null);
        }
    }
}

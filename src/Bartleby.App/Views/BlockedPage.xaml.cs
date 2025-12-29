using Bartleby.App.ViewModels;

namespace Bartleby.App.Views;

public partial class BlockedPage : ContentPage
{
    public BlockedPage(BlockedViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is BlockedViewModel vm)
        {
            await vm.LoadQuestionsCommand.ExecuteAsync(null);
        }
    }
}

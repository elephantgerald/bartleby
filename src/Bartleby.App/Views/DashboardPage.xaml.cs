using Bartleby.App.ViewModels;

namespace Bartleby.App.Views;

public partial class DashboardPage : ContentPage
{
    private bool _isAnimating;
    private CancellationTokenSource? _animationCts;

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is DashboardViewModel vm)
        {
            await vm.LoadDataCommand.ExecuteAsync(null);
            UpdatePulsingAnimation(vm.HasBlockedItems);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopPulsingAnimation();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DashboardViewModel.HasBlockedItems) &&
            sender is DashboardViewModel vm)
        {
            MainThread.BeginInvokeOnMainThread(() => UpdatePulsingAnimation(vm.HasBlockedItems));
        }
    }

    private void UpdatePulsingAnimation(bool shouldAnimate)
    {
        if (shouldAnimate && !_isAnimating)
        {
            StartPulsingAnimation();
        }
        else if (!shouldAnimate && _isAnimating)
        {
            StopPulsingAnimation();
        }
    }

    private void StartPulsingAnimation()
    {
        _isAnimating = true;
        _animationCts = new CancellationTokenSource();
        _ = RunPulsingAnimationAsync(_animationCts.Token);
    }

    private void StopPulsingAnimation()
    {
        _isAnimating = false;
        _animationCts?.Cancel();
        _animationCts?.Dispose();
        _animationCts = null;
        BlockedFrame.Scale = 1.0;
    }

    private async Task RunPulsingAnimationAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await BlockedFrame.ScaleTo(1.05, 500, Easing.SinInOut);
                if (cancellationToken.IsCancellationRequested) break;
                await BlockedFrame.ScaleTo(1.0, 500, Easing.SinInOut);
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when animation is stopped
        }
    }
}

namespace Bartleby.App;

public partial class AppShell : Shell
{
    private string _currentRoute = "Dashboard";

    public AppShell()
    {
        InitializeComponent();
        UpdateVisualState();
    }

    private async void OnDashboardTapped(object? sender, TappedEventArgs e)
    {
        await NavigateToRoute("Dashboard");
    }

    private async void OnWorkItemsTapped(object? sender, TappedEventArgs e)
    {
        await NavigateToRoute("WorkItems");
    }

    private async void OnBlockedTapped(object? sender, TappedEventArgs e)
    {
        await NavigateToRoute("Blocked");
    }

    private async void OnSettingsTapped(object? sender, TappedEventArgs e)
    {
        await NavigateToRoute("Settings");
    }

    private async Task NavigateToRoute(string route)
    {
        if (_currentRoute == route)
            return;

        _currentRoute = route;
        UpdateVisualState();
        await GoToAsync($"//{route}");
    }

    private void UpdateVisualState()
    {
        // Get theme-appropriate colors
        var activeFill = Application.Current?.RequestedTheme == AppTheme.Dark
            ? (Color)Application.Current.Resources["White"]
            : (Color)Application.Current!.Resources["Primary"];

        var inactiveFill = Application.Current?.RequestedTheme == AppTheme.Dark
            ? (Color)Application.Current.Resources["Gray400"]
            : (Color)Application.Current!.Resources["Gray500"];

        // Dashboard
        DashboardIndicator.IsVisible = _currentRoute == "Dashboard";
        DashboardIcon.Fill = new SolidColorBrush(_currentRoute == "Dashboard" ? activeFill : inactiveFill);

        // Work Items
        WorkItemsIndicator.IsVisible = _currentRoute == "WorkItems";
        WorkItemsIcon.Fill = new SolidColorBrush(_currentRoute == "WorkItems" ? activeFill : inactiveFill);

        // Blocked
        BlockedIndicator.IsVisible = _currentRoute == "Blocked";
        BlockedIcon.Fill = new SolidColorBrush(_currentRoute == "Blocked" ? activeFill : inactiveFill);

        // Settings
        SettingsIndicator.IsVisible = _currentRoute == "Settings";
        SettingsIcon.Fill = new SolidColorBrush(_currentRoute == "Settings" ? activeFill : inactiveFill);
    }
}

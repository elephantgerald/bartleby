using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;

namespace Bartleby.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IAIProvider _aiProvider;
    private readonly IWorkSource _workSource;

    // Azure OpenAI
    [ObservableProperty]
    public partial string AzureOpenAIEndpoint { get; set; }

    [ObservableProperty]
    public partial string AzureOpenAIApiKey { get; set; }

    [ObservableProperty]
    public partial string AzureOpenAIDeploymentName { get; set; }

    // GitHub
    [ObservableProperty]
    public partial string GitHubToken { get; set; }

    [ObservableProperty]
    public partial string GitHubOwner { get; set; }

    [ObservableProperty]
    public partial string GitHubRepo { get; set; }

    // Orchestrator
    [ObservableProperty]
    public partial bool OrchestratorEnabled { get; set; }

    [ObservableProperty]
    public partial int OrchestratorIntervalMinutes { get; set; }

    // Status
    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public SettingsViewModel(
        ISettingsRepository settingsRepository,
        IAIProvider aiProvider,
        IWorkSource workSource)
    {
        _settingsRepository = settingsRepository;
        _aiProvider = aiProvider;
        _workSource = workSource;
        AzureOpenAIEndpoint = string.Empty;
        AzureOpenAIApiKey = string.Empty;
        AzureOpenAIDeploymentName = string.Empty;
        GitHubToken = string.Empty;
        GitHubOwner = string.Empty;
        GitHubRepo = string.Empty;
        OrchestratorIntervalMinutes = 5;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        IsLoading = true;

        try
        {
            var settings = await _settingsRepository.GetSettingsAsync();

            AzureOpenAIEndpoint = settings.AzureOpenAIEndpoint ?? string.Empty;
            AzureOpenAIApiKey = settings.AzureOpenAIApiKey ?? string.Empty;
            AzureOpenAIDeploymentName = settings.AzureOpenAIDeploymentName ?? string.Empty;

            GitHubToken = settings.GitHubToken ?? string.Empty;
            GitHubOwner = settings.GitHubOwner ?? string.Empty;
            GitHubRepo = settings.GitHubRepo ?? string.Empty;

            OrchestratorEnabled = settings.OrchestratorEnabled;
            OrchestratorIntervalMinutes = settings.OrchestratorIntervalMinutes;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        IsLoading = true;
        StatusMessage = "Saving...";

        try
        {
            var settings = await _settingsRepository.GetSettingsAsync();

            settings.AzureOpenAIEndpoint = AzureOpenAIEndpoint;
            settings.AzureOpenAIApiKey = AzureOpenAIApiKey;
            settings.AzureOpenAIDeploymentName = AzureOpenAIDeploymentName;

            settings.GitHubToken = GitHubToken;
            settings.GitHubOwner = GitHubOwner;
            settings.GitHubRepo = GitHubRepo;

            settings.OrchestratorEnabled = OrchestratorEnabled;
            settings.OrchestratorIntervalMinutes = OrchestratorIntervalMinutes;

            await _settingsRepository.SaveSettingsAsync(settings);
            StatusMessage = "Settings saved successfully!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task TestAIConnectionAsync()
    {
        StatusMessage = "Testing AI connection...";
        var success = await _aiProvider.TestConnectionAsync();
        StatusMessage = success ? "AI connection successful!" : "AI connection failed!";
    }

    [RelayCommand]
    private async Task TestWorkSourceConnectionAsync()
    {
        StatusMessage = "Testing work source connection...";
        var success = await _workSource.TestConnectionAsync();
        StatusMessage = success ? "Work source connection successful!" : "Work source connection failed!";
    }
}

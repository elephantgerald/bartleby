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
    private string _azureOpenAIEndpoint = string.Empty;

    [ObservableProperty]
    private string _azureOpenAIApiKey = string.Empty;

    [ObservableProperty]
    private string _azureOpenAIDeploymentName = string.Empty;

    // GitHub
    [ObservableProperty]
    private string _gitHubToken = string.Empty;

    [ObservableProperty]
    private string _gitHubOwner = string.Empty;

    [ObservableProperty]
    private string _gitHubRepo = string.Empty;

    // Orchestrator
    [ObservableProperty]
    private bool _orchestratorEnabled;

    [ObservableProperty]
    private int _orchestratorIntervalMinutes = 5;

    // Status
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public SettingsViewModel(
        ISettingsRepository settingsRepository,
        IAIProvider aiProvider,
        IWorkSource workSource)
    {
        _settingsRepository = settingsRepository;
        _aiProvider = aiProvider;
        _workSource = workSource;
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

namespace Bartleby.Core.Models;

public class AppSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Azure OpenAI Settings
    public string? AzureOpenAIEndpoint { get; set; }
    public string? AzureOpenAIApiKey { get; set; }
    public string? AzureOpenAIDeploymentName { get; set; }

    // GitHub Settings
    public string? GitHubToken { get; set; }
    public string? GitHubOwner { get; set; }
    public string? GitHubRepo { get; set; }

    // Orchestrator Settings
    public bool OrchestratorEnabled { get; set; } = false;
    public int OrchestratorIntervalMinutes { get; set; } = 5;
    public int MaxConcurrentWorkItems { get; set; } = 1;

    // Graph Settings
    public string GraphFilePath { get; set; } = "dependencies.puml";

    // Git Settings
    public string? WorkingDirectory { get; set; }
    public bool AutoCommit { get; set; } = true;
    public bool AutoPush { get; set; } = false;
}

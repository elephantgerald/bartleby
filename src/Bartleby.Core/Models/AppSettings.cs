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
    public int MaxRetryAttempts { get; set; } = 3;

    // Quiet Hours Settings
    /// <summary>
    /// Whether quiet hours are enabled.
    /// </summary>
    public bool QuietHoursEnabled { get; set; } = false;

    /// <summary>
    /// Start time for quiet hours (local time, e.g., "22:00" for 10 PM).
    /// </summary>
    public TimeOnly QuietHoursStart { get; set; } = new TimeOnly(22, 0);

    /// <summary>
    /// End time for quiet hours (local time, e.g., "07:00" for 7 AM).
    /// </summary>
    public TimeOnly QuietHoursEnd { get; set; } = new TimeOnly(7, 0);

    // Token Budget Settings
    /// <summary>
    /// Whether token budget enforcement is enabled.
    /// </summary>
    public bool TokenBudgetEnabled { get; set; } = false;

    /// <summary>
    /// Maximum tokens allowed per day. Set to 0 for unlimited.
    /// </summary>
    public int DailyTokenBudget { get; set; } = 100000;

    /// <summary>
    /// Tokens used today. Reset at midnight.
    /// </summary>
    public int TokensUsedToday { get; set; } = 0;

    /// <summary>
    /// Date when tokens were last reset.
    /// </summary>
    public DateTime TokensLastResetDate { get; set; } = DateTime.UtcNow.Date;

    // Graph Settings
    public string GraphFilePath { get; set; } = "dependencies.puml";

    // Git Settings
    public string? WorkingDirectory { get; set; }
    public bool AutoCommit { get; set; } = true;
    public bool AutoPush { get; set; } = false;
}

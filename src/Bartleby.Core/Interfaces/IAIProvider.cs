using Bartleby.Core.Models;

namespace Bartleby.Core.Interfaces;

/// <summary>
/// Interface for AI providers (Azure OpenAI, Claude, etc.).
/// </summary>
public interface IAIProvider
{
    /// <summary>
    /// Name of this AI provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes work on a work item using AI.
    /// </summary>
    Task<WorkExecutionResult> ExecuteWorkAsync(
        WorkItem workItem,
        string workingDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests the connection to the AI provider.
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an AI work execution.
/// </summary>
public class WorkExecutionResult
{
    /// <summary>
    /// Whether the work was completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The outcome of the execution.
    /// </summary>
    public WorkExecutionOutcome Outcome { get; set; }

    /// <summary>
    /// Summary of what was accomplished.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Files that were modified.
    /// </summary>
    public List<string> ModifiedFiles { get; set; } = [];

    /// <summary>
    /// Questions generated if the AI got blocked.
    /// </summary>
    public List<string> Questions { get; set; } = [];

    /// <summary>
    /// Error message if the execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Tokens used during execution.
    /// </summary>
    public int TokensUsed { get; set; }
}

public enum WorkExecutionOutcome
{
    Completed,
    Blocked,
    Failed,
    NeedsMoreContext
}

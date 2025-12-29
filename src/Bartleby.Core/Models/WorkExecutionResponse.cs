using Bartleby.Core.Interfaces;

namespace Bartleby.Core.Models;

/// <summary>
/// Result of executing work, with richer information than WorkExecutionResult.
/// Includes provenance tracking via the recorded WorkSession.
/// </summary>
public record WorkExecutionResponse
{
    /// <summary>
    /// Whether the execution succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The outcome of the execution.
    /// </summary>
    public required WorkExecutionOutcome Outcome { get; init; }

    /// <summary>
    /// The transformation type that was performed.
    /// </summary>
    public required TransformationType TransformationType { get; init; }

    /// <summary>
    /// Summary of what was accomplished.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Files that were modified.
    /// </summary>
    public IReadOnlyList<string> ModifiedFiles { get; init; } = [];

    /// <summary>
    /// Questions generated if blocked.
    /// </summary>
    public IReadOnlyList<string> Questions { get; init; } = [];

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Tokens used during execution.
    /// </summary>
    public int TokensUsed { get; init; }

    /// <summary>
    /// The work session that was recorded for this execution (provenance).
    /// </summary>
    public WorkSession? WorkSession { get; init; }
}

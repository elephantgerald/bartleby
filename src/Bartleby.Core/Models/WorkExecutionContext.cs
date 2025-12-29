namespace Bartleby.Core.Models;

/// <summary>
/// Context for executing work on a work item.
/// Aggregates all information needed for AI to perform a transformation.
/// </summary>
public record WorkExecutionContext
{
    /// <summary>
    /// The work item being worked on.
    /// </summary>
    public required WorkItem WorkItem { get; init; }

    /// <summary>
    /// The transformation type to perform.
    /// </summary>
    public required TransformationType TransformationType { get; init; }

    /// <summary>
    /// Path to the working directory (codebase).
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Previous work sessions for this item (provenance/history).
    /// </summary>
    public IReadOnlyList<WorkSession> PreviousSessions { get; init; } = [];

    /// <summary>
    /// Answered questions that provide additional context.
    /// </summary>
    public IReadOnlyList<BlockedQuestion> AnsweredQuestions { get; init; } = [];

    /// <summary>
    /// Optional additional instructions or context.
    /// </summary>
    public string? AdditionalInstructions { get; init; }
}

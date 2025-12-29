namespace Bartleby.Core.Models;

public class WorkSession
{
    /// <summary>
    /// Unique identifier for this work session.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The work item being worked on.
    /// </summary>
    public Guid WorkItemId { get; set; }

    /// <summary>
    /// When the session started.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the session ended (null if still in progress).
    /// </summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>
    /// The outcome of the session.
    /// </summary>
    public WorkSessionOutcome Outcome { get; set; } = WorkSessionOutcome.InProgress;

    /// <summary>
    /// The transformation type that was performed in this session.
    /// Used for provenance tracking.
    /// </summary>
    public TransformationType? TransformationType { get; set; }

    /// <summary>
    /// Summary of what was accomplished in this session.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Files that were modified during this session.
    /// </summary>
    public List<string> ModifiedFiles { get; set; } = [];

    /// <summary>
    /// Git commit SHA if work was committed.
    /// </summary>
    public string? CommitSha { get; set; }

    /// <summary>
    /// Tokens used during this session (for cost tracking).
    /// </summary>
    public int TokensUsed { get; set; }

    /// <summary>
    /// Any error that occurred during the session.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

public enum WorkSessionOutcome
{
    InProgress,
    Completed,
    Blocked,
    Failed
}

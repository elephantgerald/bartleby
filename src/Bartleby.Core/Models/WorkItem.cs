namespace Bartleby.Core.Models;

public class WorkItem
{
    /// <summary>
    /// Unique identifier for this work item.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Title of the work item.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the work to be done.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the work item.
    /// </summary>
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Pending;

    /// <summary>
    /// Status before the item became blocked. Used to restore state when unblocked.
    /// </summary>
    public WorkItemStatus? PreviousStatus { get; set; }

    /// <summary>
    /// Identifier from the external source (e.g., GitHub Issue number).
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Name of the source system (e.g., "GitHub", "Jira").
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// URL to the work item in the external system.
    /// </summary>
    public string? ExternalUrl { get; set; }

    /// <summary>
    /// IDs of work items that this item depends on.
    /// </summary>
    public List<Guid> Dependencies { get; set; } = [];

    /// <summary>
    /// Labels/tags associated with this work item.
    /// </summary>
    public List<string> Labels { get; set; } = [];

    /// <summary>
    /// When this work item was created locally.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this work item was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When work was last attempted on this item.
    /// </summary>
    public DateTime? LastWorkedAt { get; set; }

    /// <summary>
    /// Number of times AI has attempted to work this item.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Git branch associated with this work item.
    /// </summary>
    public string? BranchName { get; set; }

    /// <summary>
    /// Error message if the work item failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

namespace Bartleby.Core.Models;

public enum WorkItemStatus
{
    /// <summary>
    /// Work item is synced but not yet ready to be worked (dependencies not met).
    /// </summary>
    Pending,

    /// <summary>
    /// Work item is ready to be picked up by the orchestrator.
    /// </summary>
    Ready,

    /// <summary>
    /// Work item is currently being worked on by AI.
    /// </summary>
    InProgress,

    /// <summary>
    /// Work item is blocked waiting for answers to questions.
    /// </summary>
    Blocked,

    /// <summary>
    /// Work item has been completed successfully.
    /// </summary>
    Complete,

    /// <summary>
    /// Work item failed and cannot be automatically retried.
    /// </summary>
    Failed
}

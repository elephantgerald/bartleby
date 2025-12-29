namespace Bartleby.Core.Interfaces;

/// <summary>
/// Background service that orchestrates autonomous work execution.
/// </summary>
/// <remarks>
/// <para>
/// The OrchestratorService is the heart of Bartleby. It runs continuously in the background,
/// picking up ready work items and executing them through the AI provider.
/// </para>
/// <para>
/// The service respects:
/// <list type="bullet">
/// <item>Token budgets - stops when daily budget is exhausted</item>
/// <item>Quiet hours - pauses during configured time windows</item>
/// <item>Graceful shutdown - completes current work before stopping</item>
/// </list>
/// </para>
/// </remarks>
public interface IOrchestratorService
{
    /// <summary>
    /// Gets whether the orchestrator is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the current state of the orchestrator.
    /// </summary>
    OrchestratorState State { get; }

    /// <summary>
    /// Gets statistics about the current orchestrator session.
    /// </summary>
    OrchestratorStats Stats { get; }

    /// <summary>
    /// Starts the orchestrator if it's not already running.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the orchestrator gracefully, allowing current work to complete.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers an immediate work cycle instead of waiting for the timer.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task TriggerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when the orchestrator state changes.
    /// </summary>
    event EventHandler<OrchestratorStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when a work item's status changes.
    /// </summary>
    event EventHandler<WorkItemStatusChangedEventArgs>? WorkItemStatusChanged;
}

/// <summary>
/// States the orchestrator can be in.
/// </summary>
public enum OrchestratorState
{
    /// <summary>
    /// Orchestrator is stopped and not running.
    /// </summary>
    Stopped,

    /// <summary>
    /// Orchestrator is starting up.
    /// </summary>
    Starting,

    /// <summary>
    /// Orchestrator is running but idle (waiting for timer or no work available).
    /// </summary>
    Idle,

    /// <summary>
    /// Orchestrator is actively executing work.
    /// </summary>
    Working,

    /// <summary>
    /// Orchestrator is paused due to quiet hours.
    /// </summary>
    QuietHours,

    /// <summary>
    /// Orchestrator is paused due to budget exhaustion.
    /// </summary>
    BudgetExhausted,

    /// <summary>
    /// Orchestrator is stopping.
    /// </summary>
    Stopping
}

/// <summary>
/// Statistics about the orchestrator session.
/// </summary>
public class OrchestratorStats
{
    /// <summary>
    /// When the orchestrator session started.
    /// </summary>
    public DateTime? SessionStartedAt { get; set; }

    /// <summary>
    /// Number of work items completed this session.
    /// </summary>
    public int WorkItemsCompleted { get; set; }

    /// <summary>
    /// Number of work items that failed this session.
    /// </summary>
    public int WorkItemsFailed { get; set; }

    /// <summary>
    /// Number of work items currently blocked.
    /// </summary>
    public int WorkItemsBlocked { get; set; }

    /// <summary>
    /// Total tokens used this session.
    /// </summary>
    public int TokensUsedThisSession { get; set; }

    /// <summary>
    /// Total tokens used today (for budget tracking).
    /// </summary>
    public int TokensUsedToday { get; set; }

    /// <summary>
    /// Remaining token budget for today.
    /// </summary>
    public int? RemainingBudget { get; set; }

    /// <summary>
    /// ID of the work item currently being processed.
    /// </summary>
    public Guid? CurrentWorkItemId { get; set; }

    /// <summary>
    /// When the next work cycle is scheduled.
    /// </summary>
    public DateTime? NextCycleAt { get; set; }
}

/// <summary>
/// Event arguments for orchestrator state changes.
/// </summary>
public class OrchestratorStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// The previous state.
    /// </summary>
    public required OrchestratorState PreviousState { get; init; }

    /// <summary>
    /// The new state.
    /// </summary>
    public required OrchestratorState NewState { get; init; }

    /// <summary>
    /// Optional message describing the state change.
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// Event arguments for work item status changes.
/// </summary>
public class WorkItemStatusChangedEventArgs : EventArgs
{
    /// <summary>
    /// The work item ID.
    /// </summary>
    public required Guid WorkItemId { get; init; }

    /// <summary>
    /// The work item title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The previous status.
    /// </summary>
    public required Core.Models.WorkItemStatus PreviousStatus { get; init; }

    /// <summary>
    /// The new status.
    /// </summary>
    public required Core.Models.WorkItemStatus NewStatus { get; init; }

    /// <summary>
    /// Optional message describing the status change.
    /// </summary>
    public string? Message { get; init; }
}

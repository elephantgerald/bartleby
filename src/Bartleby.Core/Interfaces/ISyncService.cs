using Bartleby.Core.Models;

namespace Bartleby.Core.Interfaces;

/// <summary>
/// Orchestrates synchronization between external work sources and the local store.
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Performs a bidirectional sync between the external source and local store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing sync statistics and any errors.</returns>
    Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the timestamp of the last successful sync.
    /// </summary>
    DateTime? LastSyncTime { get; }

    /// <summary>
    /// Gets whether a sync is currently in progress.
    /// </summary>
    bool IsSyncing { get; }

    /// <summary>
    /// Raised when sync starts.
    /// </summary>
    event EventHandler<SyncStartedEventArgs>? SyncStarted;

    /// <summary>
    /// Raised when sync completes (successfully or with errors).
    /// </summary>
    event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

    /// <summary>
    /// Raised when an individual item is synced.
    /// </summary>
    event EventHandler<ItemSyncedEventArgs>? ItemSynced;
}

/// <summary>
/// Result of a sync operation.
/// </summary>
public record SyncResult
{
    /// <summary>
    /// Whether the sync completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Number of items added to the local store.
    /// </summary>
    public int ItemsAdded { get; init; }

    /// <summary>
    /// Number of items updated in the local store.
    /// </summary>
    public int ItemsUpdated { get; init; }

    /// <summary>
    /// Number of items removed from the local store.
    /// </summary>
    public int ItemsRemoved { get; init; }

    /// <summary>
    /// Number of status changes pushed back to the source.
    /// </summary>
    public int StatusesPushed { get; init; }

    /// <summary>
    /// Error message if sync failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Duration of the sync operation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Timestamp when sync completed.
    /// </summary>
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a successful sync result.
    /// </summary>
    public static SyncResult Successful(int added, int updated, int removed, int statusesPushed, TimeSpan duration) => new()
    {
        Success = true,
        ItemsAdded = added,
        ItemsUpdated = updated,
        ItemsRemoved = removed,
        StatusesPushed = statusesPushed,
        Duration = duration
    };

    /// <summary>
    /// Creates a failed sync result.
    /// </summary>
    public static SyncResult Failed(string errorMessage, TimeSpan duration) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        Duration = duration
    };
}

/// <summary>
/// Event args for sync started event.
/// </summary>
public class SyncStartedEventArgs : EventArgs
{
    /// <summary>
    /// Name of the work source being synced.
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Timestamp when sync started.
    /// </summary>
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event args for sync completed event.
/// </summary>
public class SyncCompletedEventArgs : EventArgs
{
    /// <summary>
    /// The sync result.
    /// </summary>
    public required SyncResult Result { get; init; }
}

/// <summary>
/// Event args for individual item sync.
/// </summary>
public class ItemSyncedEventArgs : EventArgs
{
    /// <summary>
    /// The work item that was synced.
    /// </summary>
    public required WorkItem WorkItem { get; init; }

    /// <summary>
    /// The action that was taken.
    /// </summary>
    public required SyncAction Action { get; init; }
}

/// <summary>
/// Action taken during sync.
/// </summary>
public enum SyncAction
{
    /// <summary>
    /// Item was added to local store.
    /// </summary>
    Added,

    /// <summary>
    /// Item was updated in local store.
    /// </summary>
    Updated,

    /// <summary>
    /// Item was removed from local store.
    /// </summary>
    Removed,

    /// <summary>
    /// Item status was pushed to source.
    /// </summary>
    StatusPushed
}

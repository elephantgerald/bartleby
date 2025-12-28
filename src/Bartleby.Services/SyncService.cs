using System.Diagnostics;
using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;

namespace Bartleby.Services;

/// <summary>
/// Orchestrates synchronization between external work sources and the local store.
/// </summary>
/// <remarks>
/// <para>
/// Sync strategy:
/// <list type="bullet">
/// <item>Pull: Fetch items from the source and update/create them locally</item>
/// <item>Status: If local status differs from source, push local status back</item>
/// <item>Content: Source wins for title, description, labels (source is truth)</item>
/// <item>Deletions: Items removed from source are removed locally</item>
/// </list>
/// </para>
/// </remarks>
public class SyncService : ISyncService
{
    private readonly IWorkSource _workSource;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly object _syncLock = new();
    private bool _isSyncing;

    public DateTime? LastSyncTime { get; private set; }

    public bool IsSyncing
    {
        get { lock (_syncLock) return _isSyncing; }
        private set { lock (_syncLock) _isSyncing = value; }
    }

    public event EventHandler<SyncStartedEventArgs>? SyncStarted;
    public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;
    public event EventHandler<ItemSyncedEventArgs>? ItemSynced;

    public SyncService(IWorkSource workSource, IWorkItemRepository workItemRepository)
    {
        _workSource = workSource ?? throw new ArgumentNullException(nameof(workSource));
        _workItemRepository = workItemRepository ?? throw new ArgumentNullException(nameof(workItemRepository));
    }

    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        // Prevent concurrent syncs
        lock (_syncLock)
        {
            if (_isSyncing)
            {
                return SyncResult.Failed("A sync is already in progress.", TimeSpan.Zero);
            }
            _isSyncing = true;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            SyncStarted?.Invoke(this, new SyncStartedEventArgs { SourceName = _workSource.Name });

            var result = await PerformSyncAsync(cancellationToken);

            stopwatch.Stop();
            var finalResult = result with { Duration = stopwatch.Elapsed };

            if (finalResult.Success)
            {
                LastSyncTime = DateTime.UtcNow;
            }

            SyncCompleted?.Invoke(this, new SyncCompletedEventArgs { Result = finalResult });

            return finalResult;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            var result = SyncResult.Failed("Sync was cancelled.", stopwatch.Elapsed);
            SyncCompleted?.Invoke(this, new SyncCompletedEventArgs { Result = result });
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var result = SyncResult.Failed($"Sync failed: {ex.Message}", stopwatch.Elapsed);
            SyncCompleted?.Invoke(this, new SyncCompletedEventArgs { Result = result });
            return result;
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private async Task<SyncResult> PerformSyncAsync(CancellationToken cancellationToken)
    {
        int itemsAdded = 0;
        int itemsUpdated = 0;
        int itemsRemoved = 0;
        int statusesPushed = 0;
        var errors = new List<string>();

        // Step 1: Fetch items from the source (with null guard - Fix #1)
        var remoteItems = ((await _workSource.SyncAsync(cancellationToken)) ?? []).ToList();

        // Step 2: Get all local items for this source (with null guard - Fix #1)
        var localItems = ((await _workItemRepository.GetAllAsync(cancellationToken)) ?? [])
            .Where(w => w.Source == _workSource.Name)
            .ToList();

        // Build lookup by ExternalId for local items (handling duplicates - Fix #2)
        // If duplicates exist, prefer the most recently updated item
        var localByExternalId = localItems
            .Where(w => !string.IsNullOrEmpty(w.ExternalId))
            .GroupBy(w => w.ExternalId!)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

        // Track which local items have been matched to remote items
        var matchedLocalIds = new HashSet<Guid>();

        // Step 3: Process remote items (add/update) with per-item error handling (Fix #7)
        foreach (var remoteItem in remoteItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(remoteItem.ExternalId))
            {
                continue; // Skip items without external ID
            }

            try
            {
                if (localByExternalId.TryGetValue(remoteItem.ExternalId, out var localItem))
                {
                    // Item exists locally - check for status difference and update
                    matchedLocalIds.Add(localItem.Id);

                    var statusDiffers = localItem.Status != remoteItem.Status;
                    var needsStatusPush = statusDiffers && ShouldPushLocalStatus(localItem.Status);

                    // Update local item with remote data, preserving local status if it should be pushed
                    var updated = MergeRemoteIntoLocal(localItem, remoteItem, preserveLocalStatus: needsStatusPush);

                    // Only update if something actually changed (Fix #3 - no-op detection)
                    if (HasContentChanged(localItem, updated))
                    {
                        await _workItemRepository.UpdateAsync(updated, cancellationToken);
                        itemsUpdated++;

                        ItemSynced?.Invoke(this, new ItemSyncedEventArgs
                        {
                            WorkItem = updated,
                            Action = SyncAction.Updated
                        });
                    }

                    // Push local status if it differs
                    if (needsStatusPush)
                    {
                        try
                        {
                            await _workSource.UpdateStatusAsync(updated, cancellationToken);
                            statusesPushed++;

                            ItemSynced?.Invoke(this, new ItemSyncedEventArgs
                            {
                                WorkItem = updated,
                                Action = SyncAction.StatusPushed
                            });
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Failed to push status for '{updated.Title}': {ex.Message}");
                        }
                    }
                }
                else
                {
                    // New item from remote - create locally
                    var created = await _workItemRepository.CreateAsync(remoteItem, cancellationToken);
                    itemsAdded++;

                    ItemSynced?.Invoke(this, new ItemSyncedEventArgs
                    {
                        WorkItem = created,
                        Action = SyncAction.Added
                    });
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to sync item '{remoteItem.Title}' (ExternalId: {remoteItem.ExternalId}): {ex.Message}");
            }
        }

        // Step 4: Remove local items that no longer exist in remote (with per-item error handling - Fix #7)
        var remoteExternalIds = remoteItems
            .Where(r => !string.IsNullOrEmpty(r.ExternalId))
            .Select(r => r.ExternalId!)
            .ToHashSet();

        foreach (var localItem in localItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!matchedLocalIds.Contains(localItem.Id) &&
                !string.IsNullOrEmpty(localItem.ExternalId) &&
                !remoteExternalIds.Contains(localItem.ExternalId))
            {
                try
                {
                    await _workItemRepository.DeleteAsync(localItem.Id, cancellationToken);
                    itemsRemoved++;

                    ItemSynced?.Invoke(this, new ItemSyncedEventArgs
                    {
                        WorkItem = localItem,
                        Action = SyncAction.Removed
                    });
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to remove item '{localItem.Title}': {ex.Message}");
                }
            }
        }

        // Return result with partial failure info if any errors occurred
        if (errors.Count > 0)
        {
            var errorSummary = $"Sync completed with {errors.Count} error(s): {string.Join("; ", errors.Take(3))}";
            if (errors.Count > 3)
            {
                errorSummary += $" ... and {errors.Count - 3} more";
            }
            return new SyncResult
            {
                Success = true, // Partial success - main sync completed
                ItemsAdded = itemsAdded,
                ItemsUpdated = itemsUpdated,
                ItemsRemoved = itemsRemoved,
                StatusesPushed = statusesPushed,
                ErrorMessage = errorSummary,
                Duration = TimeSpan.Zero
            };
        }

        return SyncResult.Successful(itemsAdded, itemsUpdated, itemsRemoved, statusesPushed, TimeSpan.Zero);
    }

    /// <summary>
    /// Determines whether a local status should be pushed back to the source.
    /// </summary>
    /// <remarks>
    /// We push status back when Bartleby has set a meaningful status.
    /// Pending is the default state, so we don't push it (let source control it).
    /// </remarks>
    private static bool ShouldPushLocalStatus(WorkItemStatus status)
    {
        return status switch
        {
            WorkItemStatus.Pending => false, // Default state, don't push
            WorkItemStatus.Ready => true,    // Bartleby determined it's ready
            WorkItemStatus.InProgress => true, // Bartleby is working on it
            WorkItemStatus.Blocked => true,  // Bartleby blocked it
            WorkItemStatus.Complete => true, // Bartleby completed it
            WorkItemStatus.Failed => true,   // Bartleby failed it
            _ => false
        };
    }

    /// <summary>
    /// Merges remote item data into local item.
    /// </summary>
    /// <param name="local">The local item to update.</param>
    /// <param name="remote">The remote item with new data.</param>
    /// <param name="preserveLocalStatus">If true, keeps local status; otherwise uses remote status.</param>
    /// <returns>The merged work item.</returns>
    private static WorkItem MergeRemoteIntoLocal(WorkItem local, WorkItem remote, bool preserveLocalStatus)
    {
        return new WorkItem
        {
            // Preserve local identity
            Id = local.Id,

            // Take content from remote (source of truth)
            Title = remote.Title,
            Description = remote.Description,
            ExternalId = remote.ExternalId,
            Source = remote.Source,
            ExternalUrl = remote.ExternalUrl,
            // Copy collections to avoid shared mutable state (Fix #4)
            Labels = remote.Labels?.ToList() ?? [],

            // Preserve or take status based on conflict resolution
            Status = preserveLocalStatus ? local.Status : remote.Status,

            // Preserve local-only fields - copy collections to avoid shared mutable state (Fix #4)
            Dependencies = local.Dependencies?.ToList() ?? [],
            CreatedAt = local.CreatedAt,
            LastWorkedAt = local.LastWorkedAt,
            AttemptCount = local.AttemptCount,
            BranchName = local.BranchName,
            ErrorMessage = local.ErrorMessage,

            // Update timestamp
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Determines whether content has changed between two work items. (Fix #3 - no-op detection)
    /// </summary>
    /// <remarks>
    /// Compares fields that come from the remote source (title, description, labels, URL)
    /// and status when applicable. Local-only fields are not compared since they don't
    /// trigger an update need.
    /// </remarks>
    private static bool HasContentChanged(WorkItem original, WorkItem updated)
    {
        // Compare content fields that come from remote
        if (original.Title != updated.Title) return true;
        if (original.Description != updated.Description) return true;
        if (original.ExternalUrl != updated.ExternalUrl) return true;
        if (original.Status != updated.Status) return true;

        // Compare labels (order-insensitive)
        var originalLabels = original.Labels ?? [];
        var updatedLabels = updated.Labels ?? [];
        if (originalLabels.Count != updatedLabels.Count) return true;
        if (!originalLabels.OrderBy(l => l).SequenceEqual(updatedLabels.OrderBy(l => l))) return true;

        return false;
    }
}

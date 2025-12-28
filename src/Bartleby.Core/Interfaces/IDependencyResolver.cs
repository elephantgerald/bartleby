using Bartleby.Core.Models;

namespace Bartleby.Core.Interfaces;

/// <summary>
/// Resolves which work items are ready to be worked on based on dependency status.
/// </summary>
public interface IDependencyResolver
{
    /// <summary>
    /// Gets work items that are ready to be worked on (all dependencies complete).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Work items ready for processing, ordered by priority.</returns>
    Task<IReadOnlyList<WorkItem>> GetReadyWorkItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific work item is ready to be worked on.
    /// </summary>
    /// <param name="workItemId">The work item ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the work item is ready, false otherwise.</returns>
    Task<bool> IsReadyAsync(Guid workItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects circular dependencies in the graph.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of cycles found, where each cycle is a list of work item IDs.</returns>
    Task<IReadOnlyList<IReadOnlyList<Guid>>> DetectCircularDependenciesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the dependency chain for a work item (what it depends on, recursively).
    /// </summary>
    /// <param name="workItemId">The work item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of work item IDs this item depends on, in dependency order.</returns>
    Task<IReadOnlyList<Guid>> GetDependencyChainAsync(Guid workItemId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of dependency resolution with diagnostic information.
/// </summary>
public class DependencyResolutionResult
{
    /// <summary>
    /// Work items that are ready to be worked on.
    /// </summary>
    public required IReadOnlyList<WorkItem> ReadyItems { get; init; }

    /// <summary>
    /// Work items that are blocked due to unmet dependencies.
    /// </summary>
    public required IReadOnlyList<WorkItem> BlockedItems { get; init; }

    /// <summary>
    /// Circular dependency cycles detected.
    /// </summary>
    public required IReadOnlyList<IReadOnlyList<Guid>> Cycles { get; init; }

    /// <summary>
    /// Work items that are part of circular dependencies.
    /// </summary>
    public required IReadOnlyList<WorkItem> CyclicItems { get; init; }

    /// <summary>
    /// Whether the resolution completed successfully without cycles.
    /// </summary>
    public bool HasCycles => Cycles.Count > 0;
}

using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;

namespace Bartleby.Services;

/// <summary>
/// Resolves which work items are ready to be worked on based on dependency status.
/// </summary>
/// <remarks>
/// <para>
/// A work item is considered "ready" when:
/// <list type="bullet">
/// <item>It has status Pending or Ready</item>
/// <item>All of its dependencies (from the graph) have status Complete</item>
/// <item>It is not part of a circular dependency</item>
/// </list>
/// </para>
/// <para>
/// Work items not present in the dependency graph are considered to have no dependencies
/// and are always ready (assuming appropriate status).
/// </para>
/// <para>
/// The resolver uses <see cref="DependencyGraph.GetWorkableItems"/> which returns nodes
/// where all dependencies have status Complete. This is the core readiness check.
/// </para>
/// </remarks>
public class DependencyResolver : IDependencyResolver
{
    private readonly IGraphStore _graphStore;
    private readonly IWorkItemRepository _workItemRepository;

    public DependencyResolver(IGraphStore graphStore, IWorkItemRepository workItemRepository)
    {
        _graphStore = graphStore ?? throw new ArgumentNullException(nameof(graphStore));
        _workItemRepository = workItemRepository ?? throw new ArgumentNullException(nameof(workItemRepository));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkItem>> GetReadyWorkItemsAsync(CancellationToken cancellationToken = default)
    {
        var context = await LoadResolutionContextAsync(cancellationToken);
        var (readyItems, _, _) = CategorizeWorkItems(context);
        return readyItems;
    }

    /// <inheritdoc />
    public async Task<bool> IsReadyAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        var graph = await _graphStore.LoadGraphAsync(cancellationToken);

        // If the item isn't in the graph, it has no dependencies and is ready
        if (!graph.Nodes.TryGetValue(workItemId, out var node))
        {
            return true;
        }

        // Check if this item is part of a cycle - cyclic items are never ready
        var cycles = DetectCycles(graph);
        var cyclicNodeIds = cycles.SelectMany(c => c).ToHashSet();
        if (cyclicNodeIds.Contains(workItemId))
        {
            return false;
        }

        // Check if all dependencies are complete
        var workItems = await _workItemRepository.GetAllAsync(cancellationToken);
        var statusLookup = workItems.ToDictionary(w => w.Id, w => w.Status);

        return node.DependsOn.All(depId =>
            statusLookup.TryGetValue(depId, out var status) && status == WorkItemStatus.Complete
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReadOnlyList<Guid>>> DetectCircularDependenciesAsync(CancellationToken cancellationToken = default)
    {
        var graph = await _graphStore.LoadGraphAsync(cancellationToken);
        return DetectCycles(graph);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Dependencies are returned in reverse-topological order (deepest dependencies first).
    /// For example, if C depends on B, and B depends on A, calling this for C returns [A, B].
    /// This order is useful for processing dependencies before dependents.
    /// </remarks>
    public async Task<IReadOnlyList<Guid>> GetDependencyChainAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        var graph = await _graphStore.LoadGraphAsync(cancellationToken);
        var chain = new List<Guid>();
        var visited = new HashSet<Guid>();

        CollectDependencies(graph, workItemId, chain, visited);

        return chain;
    }

    /// <summary>
    /// Recursively collects all dependencies for a work item in reverse-topological order.
    /// Deepest dependencies appear first in the result.
    /// </summary>
    private static void CollectDependencies(DependencyGraph graph, Guid workItemId, List<Guid> chain, HashSet<Guid> visited)
    {
        if (!graph.Nodes.TryGetValue(workItemId, out var node))
        {
            return;
        }

        foreach (var depId in node.DependsOn)
        {
            if (visited.Add(depId))
            {
                // First collect transitive dependencies (depth-first)
                CollectDependencies(graph, depId, chain, visited);
                // Then add this dependency (so deeper deps come first)
                chain.Add(depId);
            }
        }
    }

    /// <summary>
    /// Detects cycles in the dependency graph using DFS back-edge detection.
    /// </summary>
    /// <remarks>
    /// This is a simple DFS-based cycle detector that finds back-edges.
    /// It may report duplicate cycles or cycles that are rotations of each other.
    /// For more precise strongly-connected-component detection, consider Tarjan's algorithm.
    /// </remarks>
    private static List<IReadOnlyList<Guid>> DetectCycles(DependencyGraph graph)
    {
        var cycles = new List<IReadOnlyList<Guid>>();
        var visited = new HashSet<Guid>();
        var recursionStack = new HashSet<Guid>();
        var path = new List<Guid>();

        foreach (var nodeId in graph.Nodes.Keys)
        {
            if (!visited.Contains(nodeId))
            {
                DetectCyclesDfs(graph, nodeId, visited, recursionStack, path, cycles);
            }
        }

        return cycles;
    }

    /// <summary>
    /// DFS helper for cycle detection.
    /// </summary>
    private static void DetectCyclesDfs(
        DependencyGraph graph,
        Guid nodeId,
        HashSet<Guid> visited,
        HashSet<Guid> recursionStack,
        List<Guid> path,
        List<IReadOnlyList<Guid>> cycles)
    {
        visited.Add(nodeId);
        recursionStack.Add(nodeId);
        path.Add(nodeId);

        if (graph.Nodes.TryGetValue(nodeId, out var node))
        {
            foreach (var depId in node.DependsOn)
            {
                if (!visited.Contains(depId))
                {
                    DetectCyclesDfs(graph, depId, visited, recursionStack, path, cycles);
                }
                else if (recursionStack.Contains(depId))
                {
                    // Found a cycle - extract it from the path
                    var cycleStart = path.IndexOf(depId);
                    if (cycleStart >= 0)
                    {
                        var cycle = path.Skip(cycleStart).ToList();
                        cycle.Add(depId); // Close the cycle
                        cycles.Add(cycle);
                    }
                }
            }
        }

        path.RemoveAt(path.Count - 1);
        recursionStack.Remove(nodeId);
    }

    /// <summary>
    /// Performs a full dependency resolution with detailed diagnostics.
    /// </summary>
    public async Task<DependencyResolutionResult> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var context = await LoadResolutionContextAsync(cancellationToken);
        var (readyItems, blockedItems, cyclicItems) = CategorizeWorkItems(context);

        return new DependencyResolutionResult
        {
            ReadyItems = readyItems,
            BlockedItems = blockedItems,
            Cycles = context.Cycles,
            CyclicItems = cyclicItems
        };
    }

    #region Shared Resolution Logic

    /// <summary>
    /// Context containing all data needed for dependency resolution.
    /// </summary>
    private sealed class ResolutionContext
    {
        public required DependencyGraph Graph { get; init; }
        public required List<WorkItem> WorkItems { get; init; }
        public required Dictionary<Guid, WorkItemStatus> StatusLookup { get; init; }
        public required HashSet<Guid> ReadyIds { get; init; }
        public required HashSet<Guid> GraphNodeIds { get; init; }
        public required HashSet<Guid> ItemIdsNotInGraph { get; init; }
        public required HashSet<Guid> CyclicNodeIds { get; init; }
        public required List<IReadOnlyList<Guid>> Cycles { get; init; }
    }

    /// <summary>
    /// Loads all data needed for dependency resolution.
    /// </summary>
    private async Task<ResolutionContext> LoadResolutionContextAsync(CancellationToken cancellationToken)
    {
        var graph = await _graphStore.LoadGraphAsync(cancellationToken);
        var workItems = (await _workItemRepository.GetAllAsync(cancellationToken)).ToList();

        // Build status lookup
        var statusLookup = workItems.ToDictionary(w => w.Id, w => w.Status);

        // Detect cycles
        var cycles = DetectCycles(graph);
        var cyclicNodeIds = cycles.SelectMany(c => c).ToHashSet();

        // Get IDs of ready items from the graph.
        // GetWorkableItems returns nodes where all dependencies have status Complete.
        var readyIds = graph.GetWorkableItems(id =>
            statusLookup.TryGetValue(id, out var status) ? status : WorkItemStatus.Pending
        ).ToHashSet();

        // Identify items not in graph (they have no dependencies)
        var graphNodeIds = graph.Nodes.Keys.ToHashSet();
        var itemIdsNotInGraph = workItems
            .Where(w => !graphNodeIds.Contains(w.Id))
            .Where(w => w.Status == WorkItemStatus.Pending || w.Status == WorkItemStatus.Ready)
            .Select(w => w.Id)
            .ToHashSet();

        return new ResolutionContext
        {
            Graph = graph,
            WorkItems = workItems,
            StatusLookup = statusLookup,
            ReadyIds = readyIds,
            GraphNodeIds = graphNodeIds,
            ItemIdsNotInGraph = itemIdsNotInGraph,
            CyclicNodeIds = cyclicNodeIds,
            Cycles = cycles
        };
    }

    /// <summary>
    /// Categorizes work items into ready, blocked, and cyclic lists.
    /// </summary>
    private static (List<WorkItem> Ready, List<WorkItem> Blocked, List<WorkItem> Cyclic) CategorizeWorkItems(
        ResolutionContext context)
    {
        var readyItems = new List<WorkItem>();
        var blockedItems = new List<WorkItem>();
        var cyclicItems = new List<WorkItem>();

        foreach (var workItem in context.WorkItems)
        {
            // Only consider Pending/Ready items for categorization
            if (workItem.Status != WorkItemStatus.Pending && workItem.Status != WorkItemStatus.Ready)
            {
                continue;
            }

            // Cyclic items go to their own category
            if (context.CyclicNodeIds.Contains(workItem.Id))
            {
                cyclicItems.Add(workItem);
                continue;
            }

            // Check if ready (either in ready set from graph, or not in graph at all)
            var isReady = context.ReadyIds.Contains(workItem.Id) ||
                          context.ItemIdsNotInGraph.Contains(workItem.Id);

            if (isReady)
            {
                readyItems.Add(workItem);
            }
            else
            {
                blockedItems.Add(workItem);
            }
        }

        // Sort ready items by creation date (oldest first)
        readyItems = readyItems.OrderBy(w => w.CreatedAt).ToList();

        return (readyItems, blockedItems, cyclicItems);
    }

    #endregion
}

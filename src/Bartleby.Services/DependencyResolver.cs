using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;

namespace Bartleby.Services;

/// <summary>
/// Resolves which work items are ready to be worked on based on dependency status.
/// </summary>
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
        var graph = await _graphStore.LoadGraphAsync(cancellationToken);
        var workItems = (await _workItemRepository.GetAllAsync(cancellationToken)).ToList();

        // Build a lookup for work item statuses
        var statusLookup = workItems.ToDictionary(w => w.Id, w => w.Status);

        // Get IDs of ready items from the graph
        var readyIds = graph.GetWorkableItems(id =>
            statusLookup.TryGetValue(id, out var status) ? status : WorkItemStatus.Pending
        ).ToHashSet();

        // Also consider work items that are Pending/Ready but not in the graph (no dependencies)
        var graphNodeIds = graph.Nodes.Keys.ToHashSet();
        var itemsNotInGraph = workItems
            .Where(w => !graphNodeIds.Contains(w.Id))
            .Where(w => w.Status == WorkItemStatus.Pending || w.Status == WorkItemStatus.Ready);

        // Get the actual work items that are ready
        var readyFromGraph = workItems.Where(w => readyIds.Contains(w.Id));
        var allReady = readyFromGraph.Concat(itemsNotInGraph).ToList();

        // Filter to only Pending/Ready items and sort by creation date (oldest first)
        return allReady
            .Where(w => w.Status == WorkItemStatus.Pending || w.Status == WorkItemStatus.Ready)
            .OrderBy(w => w.CreatedAt)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<bool> IsReadyAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        var graph = await _graphStore.LoadGraphAsync(cancellationToken);
        var workItems = await _workItemRepository.GetAllAsync(cancellationToken);
        var statusLookup = workItems.ToDictionary(w => w.Id, w => w.Status);

        // If the item isn't in the graph, it has no dependencies and is ready
        if (!graph.Nodes.TryGetValue(workItemId, out var node))
        {
            return true;
        }

        // Check if all dependencies are complete
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
    public async Task<IReadOnlyList<Guid>> GetDependencyChainAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        var graph = await _graphStore.LoadGraphAsync(cancellationToken);
        var chain = new List<Guid>();
        var visited = new HashSet<Guid>();

        CollectDependencies(graph, workItemId, chain, visited);

        return chain;
    }

    /// <summary>
    /// Recursively collects all dependencies for a work item.
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
                // First collect transitive dependencies
                CollectDependencies(graph, depId, chain, visited);
                // Then add this dependency
                chain.Add(depId);
            }
        }
    }

    /// <summary>
    /// Detects all cycles in the dependency graph using Tarjan's algorithm.
    /// </summary>
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
        var graph = await _graphStore.LoadGraphAsync(cancellationToken);
        var workItems = (await _workItemRepository.GetAllAsync(cancellationToken)).ToList();
        var statusLookup = workItems.ToDictionary(w => w.Id, w => w.Status);

        // Detect cycles first
        var cycles = DetectCycles(graph);
        var cyclicNodeIds = cycles.SelectMany(c => c).ToHashSet();

        // Get ready items (excluding cyclic ones)
        var readyIds = graph.GetWorkableItems(id =>
            statusLookup.TryGetValue(id, out var status) ? status : WorkItemStatus.Pending
        ).ToHashSet();

        // Items not in graph are considered ready (no dependencies)
        var graphNodeIds = graph.Nodes.Keys.ToHashSet();
        var itemsNotInGraph = workItems
            .Where(w => !graphNodeIds.Contains(w.Id))
            .Where(w => w.Status == WorkItemStatus.Pending || w.Status == WorkItemStatus.Ready)
            .ToList();

        var readyItems = workItems
            .Where(w => readyIds.Contains(w.Id) || itemsNotInGraph.Contains(w))
            .Where(w => w.Status == WorkItemStatus.Pending || w.Status == WorkItemStatus.Ready)
            .Where(w => !cyclicNodeIds.Contains(w.Id))
            .OrderBy(w => w.CreatedAt)
            .ToList();

        var blockedItems = workItems
            .Where(w => !readyIds.Contains(w.Id) && !itemsNotInGraph.Contains(w))
            .Where(w => w.Status == WorkItemStatus.Pending || w.Status == WorkItemStatus.Ready)
            .Where(w => !cyclicNodeIds.Contains(w.Id))
            .ToList();

        var cyclicItems = workItems
            .Where(w => cyclicNodeIds.Contains(w.Id))
            .ToList();

        return new DependencyResolutionResult
        {
            ReadyItems = readyItems,
            BlockedItems = blockedItems,
            Cycles = cycles,
            CyclicItems = cyclicItems
        };
    }
}

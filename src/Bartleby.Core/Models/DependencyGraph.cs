namespace Bartleby.Core.Models;

public class DependencyGraph
{
    /// <summary>
    /// All nodes (work items) in the graph.
    /// </summary>
    public Dictionary<Guid, DependencyNode> Nodes { get; set; } = [];

    /// <summary>
    /// Adds a work item as a node in the graph.
    /// </summary>
    public void AddNode(WorkItem workItem)
    {
        if (!Nodes.ContainsKey(workItem.Id))
        {
            Nodes[workItem.Id] = new DependencyNode
            {
                WorkItemId = workItem.Id,
                Title = workItem.Title
            };
        }
    }

    /// <summary>
    /// Adds a dependency edge between two work items.
    /// </summary>
    public void AddDependency(Guid dependentId, Guid dependsOnId)
    {
        if (Nodes.TryGetValue(dependentId, out var node))
        {
            if (!node.DependsOn.Contains(dependsOnId))
            {
                node.DependsOn.Add(dependsOnId);
            }
        }
    }

    /// <summary>
    /// Gets all work items that have no unmet dependencies.
    /// </summary>
    public IEnumerable<Guid> GetWorkableItems(Func<Guid, WorkItemStatus> getStatus)
    {
        foreach (var node in Nodes.Values)
        {
            var status = getStatus(node.WorkItemId);
            if (status != WorkItemStatus.Ready && status != WorkItemStatus.Pending)
                continue;

            var allDependenciesMet = node.DependsOn.All(depId =>
            {
                var depStatus = getStatus(depId);
                return depStatus == WorkItemStatus.Complete;
            });

            if (allDependenciesMet)
            {
                yield return node.WorkItemId;
            }
        }
    }

    /// <summary>
    /// Removes a node from the graph.
    /// </summary>
    public void RemoveNode(Guid workItemId)
    {
        Nodes.Remove(workItemId);
        foreach (var node in Nodes.Values)
        {
            node.DependsOn.Remove(workItemId);
        }
    }
}

public class DependencyNode
{
    public Guid WorkItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<Guid> DependsOn { get; set; } = [];
}

using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;

namespace Bartleby.Infrastructure.Graph;

/// <summary>
/// PlantUML-based graph storage with full parsing support.
/// </summary>
public class PlantUmlGraphStore : IGraphStore
{
    private readonly string _filePath;
    private readonly PlantUmlParser _parser = new();
    private DependencyGraph _cachedGraph = new();

    /// <summary>
    /// Mapping from PlantUML aliases to WorkItem GUIDs.
    /// Populated during save, used during load.
    /// </summary>
    private readonly Dictionary<string, Guid> _aliasToGuid = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reverse mapping from GUID to alias.
    /// </summary>
    private readonly Dictionary<Guid, string> _guidToAlias = [];

    public PlantUmlGraphStore(string filePath = "dependencies.puml")
    {
        _filePath = filePath;
    }

    /// <summary>
    /// Gets the last parse result for diagnostics.
    /// </summary>
    public PlantUmlParseResult? LastParseResult { get; private set; }

    public async Task<DependencyGraph> LoadGraphAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return _cachedGraph;
        }

        var content = await File.ReadAllTextAsync(_filePath, cancellationToken);
        LastParseResult = _parser.Parse(content);

        if (!LastParseResult.Success)
        {
            // Return cached graph if parsing fails - errors available via LastParseResult
            return _cachedGraph;
        }

        _cachedGraph = BuildGraphFromParseResult(LastParseResult);
        return _cachedGraph;
    }

    public async Task SaveGraphAsync(DependencyGraph graph, CancellationToken cancellationToken = default)
    {
        _cachedGraph = graph;

        // Update alias mappings for all nodes
        foreach (var node in graph.Nodes.Values)
        {
            var alias = node.WorkItemId.ToString()[..8];
            _aliasToGuid[alias] = node.WorkItemId;
            _guidToAlias[node.WorkItemId] = alias;
        }

        var content = GeneratePlantUml(graph);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(_filePath, content, cancellationToken);
    }

    public async Task<string> GetRawContentAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_filePath))
        {
            return await File.ReadAllTextAsync(_filePath, cancellationToken);
        }

        return GenerateEmptyPlantUml();
    }

    public async Task SetRawContentAsync(string content, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(_filePath, content, cancellationToken);
    }

    private static string GeneratePlantUml(DependencyGraph graph)
    {
        var lines = new List<string>
        {
            "@startuml",
            "' Bartleby Dependency Graph",
            "' Auto-generated - modify with care",
            ""
        };

        foreach (var node in graph.Nodes.Values)
        {
            var shortId = node.WorkItemId.ToString()[..8];
            lines.Add($"rectangle \"{node.Title}\" as {shortId}");
        }

        lines.Add("");

        foreach (var node in graph.Nodes.Values)
        {
            var shortId = node.WorkItemId.ToString()[..8];
            foreach (var depId in node.DependsOn)
            {
                var depShortId = depId.ToString()[..8];
                lines.Add($"{depShortId} --> {shortId}");
            }
        }

        lines.Add("");
        lines.Add("@enduml");

        return string.Join(Environment.NewLine, lines);
    }

    private static string GenerateEmptyPlantUml()
    {
        return """
            @startuml
            ' Bartleby Dependency Graph
            ' Add work items and dependencies here

            @enduml
            """;
    }

    /// <summary>
    /// Builds a DependencyGraph from parsed PlantUML data.
    /// Uses existing alias mappings or creates new GUIDs for unknown aliases.
    /// </summary>
    private DependencyGraph BuildGraphFromParseResult(PlantUmlParseResult result)
    {
        var graph = new DependencyGraph();

        // First pass: create nodes and update mappings
        foreach (var (alias, node) in result.Nodes)
        {
            var guid = GetOrCreateGuidForAlias(alias);

            graph.Nodes[guid] = new DependencyNode
            {
                WorkItemId = guid,
                Title = node.Title
            };
        }

        // Second pass: add edges
        foreach (var edge in result.Edges)
        {
            if (_aliasToGuid.TryGetValue(edge.From, out var fromGuid) &&
                _aliasToGuid.TryGetValue(edge.To, out var toGuid))
            {
                graph.AddDependency(fromGuid, toGuid);
            }
        }

        return graph;
    }

    /// <summary>
    /// Gets an existing GUID for an alias, or creates a new one.
    /// Handles aliases that are GUID prefixes from previously saved graphs.
    /// </summary>
    private Guid GetOrCreateGuidForAlias(string alias)
    {
        // Check if we already have a mapping
        if (_aliasToGuid.TryGetValue(alias, out var existingGuid))
        {
            return existingGuid;
        }

        // Check if the alias is a GUID prefix we generated
        foreach (var (guid, storedAlias) in _guidToAlias)
        {
            if (storedAlias.Equals(alias, StringComparison.OrdinalIgnoreCase))
            {
                _aliasToGuid[alias] = guid;
                return guid;
            }
        }

        // Create a new GUID for this alias
        var newGuid = Guid.NewGuid();
        _aliasToGuid[alias] = newGuid;
        _guidToAlias[newGuid] = alias;
        return newGuid;
    }

    /// <summary>
    /// Registers an alias mapping for a work item.
    /// Call this before loading to preserve GUID mappings.
    /// </summary>
    public void RegisterAliasMapping(string alias, Guid workItemId)
    {
        _aliasToGuid[alias] = workItemId;
        _guidToAlias[workItemId] = alias;
    }

    /// <summary>
    /// Clears all alias mappings.
    /// </summary>
    public void ClearAliasMappings()
    {
        _aliasToGuid.Clear();
        _guidToAlias.Clear();
    }
}

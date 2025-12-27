using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;

namespace Bartleby.Infrastructure.Graph;

/// <summary>
/// PlantUML-based graph storage.
/// For MVP: just reads/writes raw PlantUML, no parsing yet.
/// </summary>
public class PlantUmlGraphStore : IGraphStore
{
    private readonly string _filePath;
    private DependencyGraph _cachedGraph = new();

    public PlantUmlGraphStore(string filePath = "dependencies.puml")
    {
        _filePath = filePath;
    }

    public async Task<DependencyGraph> LoadGraphAsync(CancellationToken cancellationToken = default)
    {
        // For MVP: return the cached graph
        // TODO: Parse PlantUML to build actual graph
        if (File.Exists(_filePath))
        {
            var content = await File.ReadAllTextAsync(_filePath, cancellationToken);
            // TODO: Parse content into _cachedGraph
        }

        return _cachedGraph;
    }

    public async Task SaveGraphAsync(DependencyGraph graph, CancellationToken cancellationToken = default)
    {
        _cachedGraph = graph;
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
}

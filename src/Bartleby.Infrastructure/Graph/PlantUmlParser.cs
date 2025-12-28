using System.Text.RegularExpressions;

namespace Bartleby.Infrastructure.Graph;

/// <summary>
/// Parses PlantUML component/object diagrams into structured data.
/// </summary>
/// <remarks>
/// Supported PlantUML subset:
/// - Node types: component, object, rectangle, node, package
/// - Syntax: [type] "Title" as alias  OR  [type] alias
/// - Arrows: -->, ..>, ==>, and reverse directions
/// - Labels: A --> B : label text
/// - Comments: ' single line comments
/// - Blocks: @startuml / @enduml
/// </remarks>
public partial class PlantUmlParser
{
    // Node patterns: component "Title" as Alias  OR  component Alias
    [GeneratedRegex(
        @"^\s*(component|object|rectangle|node|package)\s+""([^""]+)""\s+as\s+(\w+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex NodeWithTitleAndAliasRegex();

    [GeneratedRegex(
        @"^\s*(component|object|rectangle|node|package)\s+(\w+)\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex NodeWithAliasOnlyRegex();

    // Edge patterns: A --> B : label  OR  A --> B
    // Supports: -->, ..>, ==>, <--, <.., <==
    [GeneratedRegex(
        @"^\s*(\w+)\s*(--|\.\.|\=\=)(\>)\s*(\w+)(?:\s*:\s*(.+))?$")]
    private static partial Regex ForwardEdgeRegex();

    [GeneratedRegex(
        @"^\s*(\w+)\s*(\<)(--|\.\.|==)\s*(\w+)(?:\s*:\s*(.+))?$")]
    private static partial Regex ReverseEdgeRegex();

    // Block markers
    [GeneratedRegex(@"^\s*@startuml", RegexOptions.IgnoreCase)]
    private static partial Regex StartUmlRegex();

    [GeneratedRegex(@"^\s*@enduml", RegexOptions.IgnoreCase)]
    private static partial Regex EndUmlRegex();

    // Comment pattern
    [GeneratedRegex(@"^\s*'")]
    private static partial Regex CommentRegex();

    /// <summary>
    /// Parses PlantUML content into structured data.
    /// </summary>
    /// <param name="content">The PlantUML content to parse.</param>
    /// <returns>Parse result with nodes, edges, and any errors.</returns>
    public PlantUmlParseResult Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return PlantUmlParseResult.Ok([], []);
        }

        var nodes = new Dictionary<string, PlantUmlNode>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<PlantUmlEdge>();
        var errors = new List<PlantUmlParseError>();

        var lines = content.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        var inUmlBlock = false;

        // Check if file contains @startuml - if so, only parse inside blocks
        var hasUmlBlocks = lines.Any(l => StartUmlRegex().IsMatch(l.Trim()));

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var line = lines[i];
            var trimmedLine = line.Trim();

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(trimmedLine))
                continue;

            // Check for @startuml
            if (StartUmlRegex().IsMatch(trimmedLine))
            {
                if (inUmlBlock)
                {
                    errors.Add(new PlantUmlParseError
                    {
                        Message = "Unexpected @startuml - already inside a diagram block",
                        LineNumber = lineNumber,
                        LineContent = line
                    });
                }
                inUmlBlock = true;
                continue;
            }

            // Check for @enduml
            if (EndUmlRegex().IsMatch(trimmedLine))
            {
                if (!inUmlBlock)
                {
                    errors.Add(new PlantUmlParseError
                    {
                        Message = "Unexpected @enduml - not inside a diagram block",
                        LineNumber = lineNumber,
                        LineContent = line
                    });
                }
                inUmlBlock = false;
                continue;
            }

            // Skip comments
            if (CommentRegex().IsMatch(trimmedLine))
                continue;

            // Only parse content inside @startuml/@enduml blocks (or if no blocks defined)
            if (hasUmlBlocks && !inUmlBlock)
                continue;

            // Try to parse as node
            if (TryParseNode(trimmedLine, lineNumber, out var node))
            {
                if (nodes.ContainsKey(node!.Alias))
                {
                    errors.Add(new PlantUmlParseError
                    {
                        Message = $"Duplicate node alias '{node.Alias}'",
                        LineNumber = lineNumber,
                        LineContent = line
                    });
                }
                else
                {
                    nodes[node.Alias] = node;
                }
                continue;
            }

            // Try to parse as edge
            if (TryParseEdge(trimmedLine, lineNumber, out var edge))
            {
                edges.Add(edge!);
                continue;
            }

            // Line didn't match any known pattern - this is okay, PlantUML has many constructs we don't need
        }

        // Validate edges reference known nodes
        foreach (var edge in edges)
        {
            if (!nodes.ContainsKey(edge.From))
            {
                errors.Add(new PlantUmlParseError
                {
                    Message = $"Edge references unknown node '{edge.From}'",
                    LineNumber = edge.LineNumber
                });
            }
            if (!nodes.ContainsKey(edge.To))
            {
                errors.Add(new PlantUmlParseError
                {
                    Message = $"Edge references unknown node '{edge.To}'",
                    LineNumber = edge.LineNumber
                });
            }
        }

        // Check for unclosed block
        if (hasUmlBlocks && inUmlBlock)
        {
            errors.Add(new PlantUmlParseError
            {
                Message = "Missing @enduml - diagram block was not closed",
                LineNumber = lines.Length
            });
        }

        return errors.Count > 0
            ? new PlantUmlParseResult { Success = false, Nodes = nodes, Edges = edges, Errors = errors }
            : PlantUmlParseResult.Ok(nodes, edges);
    }

    private static bool TryParseNode(string line, int lineNumber, out PlantUmlNode? node)
    {
        node = null;

        // Try: component "Title" as Alias
        var match = NodeWithTitleAndAliasRegex().Match(line);
        if (match.Success)
        {
            node = new PlantUmlNode
            {
                Type = ParseNodeType(match.Groups[1].Value),
                Title = match.Groups[2].Value,
                Alias = match.Groups[3].Value,
                LineNumber = lineNumber
            };
            return true;
        }

        // Try: component Alias
        match = NodeWithAliasOnlyRegex().Match(line);
        if (match.Success)
        {
            var alias = match.Groups[2].Value;
            node = new PlantUmlNode
            {
                Type = ParseNodeType(match.Groups[1].Value),
                Title = alias, // Use alias as title when no explicit title
                Alias = alias,
                LineNumber = lineNumber
            };
            return true;
        }

        return false;
    }

    private static bool TryParseEdge(string line, int lineNumber, out PlantUmlEdge? edge)
    {
        edge = null;

        // Try forward arrow: A --> B : label
        var match = ForwardEdgeRegex().Match(line);
        if (match.Success)
        {
            edge = new PlantUmlEdge
            {
                From = match.Groups[1].Value,
                To = match.Groups[4].Value,
                ArrowType = ParseArrowType(match.Groups[2].Value),
                Label = match.Groups[5].Success ? match.Groups[5].Value.Trim() : null,
                LineNumber = lineNumber
            };
            return true;
        }

        // Try reverse arrow: A <-- B : label (B depends on A)
        match = ReverseEdgeRegex().Match(line);
        if (match.Success)
        {
            edge = new PlantUmlEdge
            {
                From = match.Groups[4].Value, // Reversed
                To = match.Groups[1].Value,   // Reversed
                ArrowType = ParseArrowType(match.Groups[3].Value),
                Label = match.Groups[5].Success ? match.Groups[5].Value.Trim() : null,
                LineNumber = lineNumber
            };
            return true;
        }

        return false;
    }

    private static PlantUmlNodeType ParseNodeType(string type) =>
        type.ToLowerInvariant() switch
        {
            "component" => PlantUmlNodeType.Component,
            "object" => PlantUmlNodeType.Object,
            "rectangle" => PlantUmlNodeType.Rectangle,
            "node" => PlantUmlNodeType.Node,
            "package" => PlantUmlNodeType.Package,
            _ => PlantUmlNodeType.Component
        };

    private static PlantUmlArrowType ParseArrowType(string arrow) =>
        arrow switch
        {
            ".." => PlantUmlArrowType.Dashed,
            "==" => PlantUmlArrowType.Bold,
            _ => PlantUmlArrowType.Solid
        };
}

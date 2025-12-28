namespace Bartleby.Infrastructure.Graph;

/// <summary>
/// Result of parsing a PlantUML document.
/// </summary>
public class PlantUmlParseResult
{
    /// <summary>
    /// Whether parsing succeeded without errors.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Parsed nodes (components, objects, rectangles, etc.).
    /// Key is the alias, value is the display title.
    /// </summary>
    public Dictionary<string, PlantUmlNode> Nodes { get; init; } = [];

    /// <summary>
    /// Parsed dependency edges.
    /// </summary>
    public List<PlantUmlEdge> Edges { get; init; } = [];

    /// <summary>
    /// Parse errors encountered.
    /// </summary>
    public List<PlantUmlParseError> Errors { get; init; } = [];

    /// <summary>
    /// Creates a successful parse result.
    /// </summary>
    public static PlantUmlParseResult Ok(
        Dictionary<string, PlantUmlNode> nodes,
        List<PlantUmlEdge> edges) => new()
    {
        Success = true,
        Nodes = nodes,
        Edges = edges
    };

    /// <summary>
    /// Creates a failed parse result with errors.
    /// </summary>
    public static PlantUmlParseResult Failed(List<PlantUmlParseError> errors) => new()
    {
        Success = false,
        Errors = errors
    };
}

/// <summary>
/// A node parsed from PlantUML (component, object, rectangle, etc.).
/// </summary>
public class PlantUmlNode
{
    /// <summary>
    /// The alias used to reference this node (e.g., "A" in "component X as A").
    /// </summary>
    public required string Alias { get; init; }

    /// <summary>
    /// The display title (e.g., "Task A" in component "Task A" as A).
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The node type (component, object, rectangle, etc.).
    /// </summary>
    public PlantUmlNodeType Type { get; init; } = PlantUmlNodeType.Component;

    /// <summary>
    /// Line number where this node was defined.
    /// </summary>
    public int LineNumber { get; init; }
}

/// <summary>
/// Types of nodes that can appear in PlantUML.
/// </summary>
public enum PlantUmlNodeType
{
    Component,
    Object,
    Rectangle,
    Node,
    Package
}

/// <summary>
/// A dependency edge between two nodes.
/// </summary>
public class PlantUmlEdge
{
    /// <summary>
    /// Source node alias (the dependent).
    /// </summary>
    public required string From { get; init; }

    /// <summary>
    /// Target node alias (the dependency).
    /// </summary>
    public required string To { get; init; }

    /// <summary>
    /// Optional label on the edge.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// The arrow type used.
    /// </summary>
    public PlantUmlArrowType ArrowType { get; init; } = PlantUmlArrowType.Solid;

    /// <summary>
    /// Line number where this edge was defined.
    /// </summary>
    public int LineNumber { get; init; }
}

/// <summary>
/// Types of arrows in PlantUML.
/// </summary>
public enum PlantUmlArrowType
{
    /// <summary>Solid arrow: --></summary>
    Solid,
    /// <summary>Dashed arrow: ..></summary>
    Dashed,
    /// <summary>Bold arrow: ==></summary>
    Bold
}

/// <summary>
/// A parse error with location information.
/// </summary>
public class PlantUmlParseError
{
    /// <summary>
    /// Error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Line number where the error occurred (1-based).
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// The problematic line content.
    /// </summary>
    public string? LineContent { get; init; }

    public override string ToString() =>
        LineNumber > 0
            ? $"Line {LineNumber}: {Message}"
            : Message;
}

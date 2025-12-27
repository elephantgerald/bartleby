using Bartleby.Core.Models;

namespace Bartleby.Core.Interfaces;

/// <summary>
/// Interface for dependency graph storage (PlantUML, etc.).
/// </summary>
public interface IGraphStore
{
    /// <summary>
    /// Loads the dependency graph from storage.
    /// </summary>
    Task<DependencyGraph> LoadGraphAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the dependency graph to storage.
    /// </summary>
    Task SaveGraphAsync(DependencyGraph graph, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the raw graph content (e.g., PlantUML text).
    /// </summary>
    Task<string> GetRawContentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the raw graph content (e.g., PlantUML text).
    /// </summary>
    Task SetRawContentAsync(string content, CancellationToken cancellationToken = default);
}

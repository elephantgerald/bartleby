using Bartleby.Core.Models;

namespace Bartleby.Core.Interfaces;

/// <summary>
/// Interface for external work item sources (GitHub, Jira, etc.).
/// </summary>
public interface IWorkSource
{
    /// <summary>
    /// Name of this work source (e.g., "GitHub", "Jira").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Syncs work items from the external source.
    /// </summary>
    Task<IEnumerable<WorkItem>> SyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of a work item in the external source.
    /// </summary>
    Task UpdateStatusAsync(WorkItem workItem, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a comment to the work item in the external source.
    /// </summary>
    Task AddCommentAsync(WorkItem workItem, string comment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests the connection to the external source.
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

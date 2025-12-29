using Bartleby.Core.Models;

namespace Bartleby.Core.Interfaces;

/// <summary>
/// Orchestrates AI work execution with structured prompts and response handling.
/// </summary>
public interface IWorkExecutor
{
    /// <summary>
    /// Executes a transformation on a work item.
    /// </summary>
    /// <param name="context">The execution context with all required information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution response with provenance recorded.</returns>
    Task<WorkExecutionResponse> ExecuteAsync(
        WorkExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds execution context for a work item.
    /// </summary>
    /// <param name="workItemId">The work item ID.</param>
    /// <param name="transformationType">The transformation to perform.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The built context, or null if work item not found.</returns>
    Task<WorkExecutionContext?> BuildContextAsync(
        Guid workItemId,
        TransformationType transformationType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines the next recommended transformation type for a work item.
    /// </summary>
    /// <param name="workItemId">The work item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The recommended transformation type.</returns>
    Task<TransformationType> GetNextTransformationAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default);
}

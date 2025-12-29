namespace Bartleby.Core.Models;

/// <summary>
/// Types of transformations that Bartleby can perform on work items.
/// Based on the Registrar's Desk narrative - each transformation has provenance.
/// </summary>
public enum TransformationType
{
    /// <summary>
    /// Interpret the work item requirements and clarify scope.
    /// </summary>
    Interpret,

    /// <summary>
    /// Create an implementation plan with steps.
    /// </summary>
    Plan,

    /// <summary>
    /// Execute the implementation plan and produce code changes.
    /// </summary>
    Execute,

    /// <summary>
    /// Refine the implementation based on feedback or test results.
    /// </summary>
    Refine,

    /// <summary>
    /// Ask clarifying questions when blocked.
    /// </summary>
    AskClarification,

    /// <summary>
    /// Finalize and verify the completed work.
    /// </summary>
    Finalize
}

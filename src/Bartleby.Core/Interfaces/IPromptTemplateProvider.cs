using Bartleby.Core.Models;

namespace Bartleby.Core.Interfaces;

/// <summary>
/// Provides prompt templates for AI transformations.
/// </summary>
public interface IPromptTemplateProvider
{
    /// <summary>
    /// Gets the system prompt for a transformation type.
    /// </summary>
    /// <param name="type">The transformation type.</param>
    /// <param name="workingDirectory">The working directory path.</param>
    /// <returns>The system prompt for the AI.</returns>
    string GetSystemPrompt(TransformationType type, string workingDirectory);

    /// <summary>
    /// Builds the user prompt with work item details and context.
    /// </summary>
    /// <param name="context">The work execution context.</param>
    /// <returns>The user prompt for the AI.</returns>
    string BuildUserPrompt(WorkExecutionContext context);
}

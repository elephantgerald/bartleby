using System.Text;
using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;

namespace Bartleby.Services.Prompts;

/// <summary>
/// Provides prompt templates for different transformation types.
/// Each transformation has a specific focus and expected output format.
/// </summary>
public class PromptTemplateProvider : IPromptTemplateProvider
{
    /// <inheritdoc />
    public string GetSystemPrompt(TransformationType type, string workingDirectory)
    {
        var basePrompt = $$"""
            You are Bartleby, an autonomous software development scrivener.
            You work methodically, with provenance and parsimony.

            Working directory: {{workingDirectory}}

            Always respond in JSON format:
            {
                "outcome": "completed" | "blocked" | "needs_context",
                "summary": "Brief description of what was accomplished or why blocked",
                "modified_files": ["list", "of", "modified", "files"],
                "questions": ["list of questions if blocked"]
            }
            """;

        var typeSpecificPrompt = type switch
        {
            TransformationType.Interpret => """

                TRANSFORMATION: Interpret
                Your task is to interpret and clarify the work item requirements.
                - Identify the core objective
                - List any assumptions you're making
                - Identify any ambiguities that need clarification
                - Output questions if requirements are unclear

                Focus on understanding WHAT needs to be done before HOW.
                """,

            TransformationType.Plan => """

                TRANSFORMATION: Plan
                Your task is to create an implementation plan.
                - Break down the work into discrete steps
                - Identify files that will need to be created or modified
                - Note any dependencies or prerequisites
                - Estimate complexity of each step

                Create a clear, actionable plan that can be executed step by step.
                """,

            TransformationType.Execute => """

                TRANSFORMATION: Execute
                Your task is to implement the planned changes.
                - Follow the implementation plan from previous sessions
                - Create or modify the necessary files
                - Write clean, testable code following project conventions
                - Document what was changed and why

                Focus on producing working code that meets the requirements.
                """,

            TransformationType.Refine => """

                TRANSFORMATION: Refine
                Your task is to refine the implementation.
                - Review previous work and identify issues
                - Fix any bugs or problems identified
                - Improve code quality where possible
                - Ensure tests pass

                Polish the implementation to production quality.
                """,

            TransformationType.AskClarification => """

                TRANSFORMATION: Ask Clarification
                Your task is to formulate clarifying questions.
                - Review what is blocking progress
                - Create specific, answerable questions
                - Prioritize questions by importance
                - Explain why each question is needed

                The questions should unblock further progress.
                """,

            TransformationType.Finalize => """

                TRANSFORMATION: Finalize
                Your task is to finalize and verify the work.
                - Verify all requirements are met
                - Ensure code is clean and documented
                - Confirm tests pass
                - Summarize what was accomplished

                Prepare the work for review and completion.
                """,

            _ => string.Empty
        };

        return basePrompt + typeSpecificPrompt;
    }

    /// <inheritdoc />
    public string BuildUserPrompt(WorkExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sb = new StringBuilder();

        // Work item header
        sb.AppendLine($"# Work Item: {context.WorkItem.Title}");
        sb.AppendLine();

        // Description
        sb.AppendLine("## Description");
        sb.AppendLine(context.WorkItem.Description);
        sb.AppendLine();

        // Labels if present
        if (context.WorkItem.Labels.Count > 0)
        {
            sb.AppendLine($"## Labels: {string.Join(", ", context.WorkItem.Labels)}");
            sb.AppendLine();
        }

        // External reference if present
        if (!string.IsNullOrEmpty(context.WorkItem.ExternalUrl))
        {
            sb.AppendLine($"## Reference: {context.WorkItem.ExternalUrl}");
            sb.AppendLine();
        }

        // Previous sessions (provenance history)
        if (context.PreviousSessions.Count > 0)
        {
            sb.AppendLine("## Previous Work Sessions (Provenance)");
            foreach (var session in context.PreviousSessions)
            {
                var transformationLabel = session.TransformationType?.ToString() ?? "Unknown";
                sb.AppendLine($"- [{session.StartedAt:yyyy-MM-dd HH:mm}] {transformationLabel} - {session.Outcome}: {session.Summary}");
                if (session.ModifiedFiles.Count > 0)
                {
                    sb.AppendLine($"  Modified: {string.Join(", ", session.ModifiedFiles)}");
                }
            }
            sb.AppendLine();
        }

        // Answered questions
        if (context.AnsweredQuestions.Count > 0)
        {
            sb.AppendLine("## Answered Questions");
            foreach (var q in context.AnsweredQuestions)
            {
                sb.AppendLine($"Q: {q.Question}");
                sb.AppendLine($"A: {q.Answer}");
                sb.AppendLine();
            }
        }

        // Additional instructions if present
        if (!string.IsNullOrEmpty(context.AdditionalInstructions))
        {
            sb.AppendLine("## Additional Instructions");
            sb.AppendLine(context.AdditionalInstructions);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

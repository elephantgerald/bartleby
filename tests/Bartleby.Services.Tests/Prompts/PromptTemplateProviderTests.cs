using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Bartleby.Services.Prompts;

namespace Bartleby.Services.Tests.Prompts;

public class PromptTemplateProviderTests
{
    private readonly IPromptTemplateProvider _sut = new PromptTemplateProvider();

    #region GetSystemPrompt Tests

    [Theory]
    [InlineData(TransformationType.Interpret)]
    [InlineData(TransformationType.Plan)]
    [InlineData(TransformationType.Execute)]
    [InlineData(TransformationType.Refine)]
    [InlineData(TransformationType.AskClarification)]
    [InlineData(TransformationType.Finalize)]
    public void GetSystemPrompt_ContainsWorkingDirectory(TransformationType type)
    {
        // Arrange
        var workingDirectory = "/test/project/path";

        // Act
        var prompt = _sut.GetSystemPrompt(type, workingDirectory);

        // Assert
        Assert.Contains(workingDirectory, prompt);
    }

    [Theory]
    [InlineData(TransformationType.Interpret)]
    [InlineData(TransformationType.Plan)]
    [InlineData(TransformationType.Execute)]
    [InlineData(TransformationType.Refine)]
    [InlineData(TransformationType.AskClarification)]
    [InlineData(TransformationType.Finalize)]
    public void GetSystemPrompt_ContainsJsonFormat(TransformationType type)
    {
        // Act
        var prompt = _sut.GetSystemPrompt(type, "/work");

        // Assert
        Assert.Contains("outcome", prompt);
        Assert.Contains("summary", prompt);
        Assert.Contains("modified_files", prompt);
        Assert.Contains("questions", prompt);
    }

    [Fact]
    public void GetSystemPrompt_ForInterpret_ContainsInterpretInstructions()
    {
        // Act
        var prompt = _sut.GetSystemPrompt(TransformationType.Interpret, "/work");

        // Assert
        Assert.Contains("TRANSFORMATION: Interpret", prompt);
        Assert.Contains("interpret", prompt);
        Assert.Contains("clarify", prompt);
    }

    [Fact]
    public void GetSystemPrompt_ForPlan_ContainsPlanInstructions()
    {
        // Act
        var prompt = _sut.GetSystemPrompt(TransformationType.Plan, "/work");

        // Assert
        Assert.Contains("TRANSFORMATION: Plan", prompt);
        Assert.Contains("implementation plan", prompt);
        Assert.Contains("steps", prompt);
    }

    [Fact]
    public void GetSystemPrompt_ForExecute_ContainsExecuteInstructions()
    {
        // Act
        var prompt = _sut.GetSystemPrompt(TransformationType.Execute, "/work");

        // Assert
        Assert.Contains("TRANSFORMATION: Execute", prompt);
        Assert.Contains("implement", prompt);
    }

    [Fact]
    public void GetSystemPrompt_ForRefine_ContainsRefineInstructions()
    {
        // Act
        var prompt = _sut.GetSystemPrompt(TransformationType.Refine, "/work");

        // Assert
        Assert.Contains("TRANSFORMATION: Refine", prompt);
        Assert.Contains("refine", prompt);
    }

    [Fact]
    public void GetSystemPrompt_ForAskClarification_ContainsClarificationInstructions()
    {
        // Act
        var prompt = _sut.GetSystemPrompt(TransformationType.AskClarification, "/work");

        // Assert
        Assert.Contains("TRANSFORMATION: Ask Clarification", prompt);
        Assert.Contains("questions", prompt);
    }

    [Fact]
    public void GetSystemPrompt_ForFinalize_ContainsFinalizeInstructions()
    {
        // Act
        var prompt = _sut.GetSystemPrompt(TransformationType.Finalize, "/work");

        // Assert
        Assert.Contains("TRANSFORMATION: Finalize", prompt);
        Assert.Contains("verify", prompt);
    }

    [Fact]
    public void GetSystemPrompt_ContainsBartlebyIdentity()
    {
        // Act
        var prompt = _sut.GetSystemPrompt(TransformationType.Execute, "/work");

        // Assert
        Assert.Contains("Bartleby", prompt);
        Assert.Contains("provenance", prompt);
        Assert.Contains("parsimony", prompt);
    }

    #endregion

    #region BuildUserPrompt Tests

    [Fact]
    public void BuildUserPrompt_WithNullContext_ThrowsArgumentNullException()
    {
        var act = () => _sut.BuildUserPrompt(null!);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void BuildUserPrompt_IncludesWorkItemTitle()
    {
        // Arrange
        var context = CreateContext("Test Feature Implementation");

        // Act
        var prompt = _sut.BuildUserPrompt(context);

        // Assert
        Assert.Contains("Test Feature Implementation", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesWorkItemDescription()
    {
        // Arrange
        var context = CreateContext();
        context.WorkItem.Description = "Implement the new authentication feature";

        // Act
        var prompt = _sut.BuildUserPrompt(context);

        // Assert
        Assert.Contains("Implement the new authentication feature", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesLabels()
    {
        // Arrange
        var context = CreateContext();
        context.WorkItem.Labels = ["feature", "auth", "priority-high"];

        // Act
        var prompt = _sut.BuildUserPrompt(context);

        // Assert
        Assert.Contains("feature", prompt);
        Assert.Contains("auth", prompt);
        Assert.Contains("priority-high", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesExternalUrl()
    {
        // Arrange
        var context = CreateContext();
        context.WorkItem.ExternalUrl = "https://github.com/test/repo/issues/123";

        // Act
        var prompt = _sut.BuildUserPrompt(context);

        // Assert
        Assert.Contains("https://github.com/test/repo/issues/123", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesPreviousSessions()
    {
        // Arrange
        var context = new WorkExecutionContext
        {
            WorkItem = CreateWorkItem(),
            TransformationType = TransformationType.Execute,
            WorkingDirectory = "/work",
            PreviousSessions =
            [
                new WorkSession
                {
                    StartedAt = new DateTime(2024, 1, 15, 10, 30, 0),
                    Outcome = WorkSessionOutcome.Completed,
                    TransformationType = TransformationType.Plan,
                    Summary = "Created implementation plan",
                    ModifiedFiles = ["PLAN.md"]
                }
            ]
        };

        // Act
        var prompt = _sut.BuildUserPrompt(context);

        // Assert
        Assert.Contains("Previous Work Sessions", prompt);
        Assert.Contains("Plan", prompt);
        Assert.Contains("Completed", prompt);
        Assert.Contains("Created implementation plan", prompt);
        Assert.Contains("PLAN.md", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesAnsweredQuestions()
    {
        // Arrange
        var context = new WorkExecutionContext
        {
            WorkItem = CreateWorkItem(),
            TransformationType = TransformationType.Execute,
            WorkingDirectory = "/work",
            AnsweredQuestions =
            [
                new BlockedQuestion
                {
                    Question = "Which database should we use?",
                    Answer = "PostgreSQL"
                },
                new BlockedQuestion
                {
                    Question = "Should we use ORM?",
                    Answer = "Yes, use Entity Framework"
                }
            ]
        };

        // Act
        var prompt = _sut.BuildUserPrompt(context);

        // Assert
        Assert.Contains("Answered Questions", prompt);
        Assert.Contains("Which database should we use?", prompt);
        Assert.Contains("PostgreSQL", prompt);
        Assert.Contains("Should we use ORM?", prompt);
        Assert.Contains("Yes, use Entity Framework", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesAdditionalInstructions()
    {
        // Arrange
        var context = new WorkExecutionContext
        {
            WorkItem = CreateWorkItem(),
            TransformationType = TransformationType.Execute,
            WorkingDirectory = "/work",
            AdditionalInstructions = "Focus on performance optimization"
        };

        // Act
        var prompt = _sut.BuildUserPrompt(context);

        // Assert
        Assert.Contains("Additional Instructions", prompt);
        Assert.Contains("Focus on performance optimization", prompt);
    }

    [Fact]
    public void BuildUserPrompt_WithEmptyOptionalFields_DoesNotIncludeHeaders()
    {
        // Arrange
        var context = new WorkExecutionContext
        {
            WorkItem = new WorkItem
            {
                Title = "Test",
                Description = "Description",
                Labels = []
            },
            TransformationType = TransformationType.Execute,
            WorkingDirectory = "/work",
            PreviousSessions = [],
            AnsweredQuestions = []
        };

        // Act
        var prompt = _sut.BuildUserPrompt(context);

        // Assert
        Assert.DoesNotContain("Labels:", prompt);
        Assert.DoesNotContain("Previous Work Sessions", prompt);
        Assert.DoesNotContain("Answered Questions", prompt);
        Assert.DoesNotContain("Additional Instructions", prompt);
    }

    #endregion

    #region Helper Methods

    private static WorkExecutionContext CreateContext(string title = "Test Work Item")
    {
        return new WorkExecutionContext
        {
            WorkItem = new WorkItem
            {
                Id = Guid.NewGuid(),
                Title = title,
                Description = "Test description"
            },
            TransformationType = TransformationType.Execute,
            WorkingDirectory = "/test/work"
        };
    }

    private static WorkItem CreateWorkItem()
    {
        return new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Test Work Item",
            Description = "Test description"
        };
    }

    #endregion
}

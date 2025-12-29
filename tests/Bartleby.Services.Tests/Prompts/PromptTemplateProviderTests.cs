using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Bartleby.Services.Prompts;
using FluentAssertions;

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
        prompt.Should().Contain(workingDirectory);
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
        prompt.Should().Contain("outcome");
        prompt.Should().Contain("summary");
        prompt.Should().Contain("modified_files");
        prompt.Should().Contain("questions");
    }

    [Fact]
    public void GetSystemPrompt_ForInterpret_ContainsInterpretInstructions()
    {
        // Act
        var prompt = _sut.GetSystemPrompt(TransformationType.Interpret, "/work");

        // Assert
        prompt.Should().Contain("TRANSFORMATION: Interpret");
        prompt.Should().Contain("interpret");
        prompt.Should().Contain("clarify");
    }

    [Fact]
    public void GetSystemPrompt_ForPlan_ContainsPlanInstructions()
    {
        // Act
        var prompt = _sut.GetSystemPrompt(TransformationType.Plan, "/work");

        // Assert
        prompt.Should().Contain("TRANSFORMATION: Plan");
        prompt.Should().Contain("implementation plan");
        prompt.Should().Contain("steps");
    }

    [Fact]
    public void GetSystemPrompt_ForExecute_ContainsExecuteInstructions()
    {
        // Act
        var prompt = _sut.GetSystemPrompt(TransformationType.Execute, "/work");

        // Assert
        prompt.Should().Contain("TRANSFORMATION: Execute");
        prompt.Should().Contain("implement");
    }

    [Fact]
    public void GetSystemPrompt_ForRefine_ContainsRefineInstructions()
    {
        // Act
        var prompt = _sut.GetSystemPrompt(TransformationType.Refine, "/work");

        // Assert
        prompt.Should().Contain("TRANSFORMATION: Refine");
        prompt.Should().Contain("refine");
    }

    [Fact]
    public void GetSystemPrompt_ForAskClarification_ContainsClarificationInstructions()
    {
        // Act
        var prompt = _sut.GetSystemPrompt(TransformationType.AskClarification, "/work");

        // Assert
        prompt.Should().Contain("TRANSFORMATION: Ask Clarification");
        prompt.Should().Contain("questions");
    }

    [Fact]
    public void GetSystemPrompt_ForFinalize_ContainsFinalizeInstructions()
    {
        // Act
        var prompt = _sut.GetSystemPrompt(TransformationType.Finalize, "/work");

        // Assert
        prompt.Should().Contain("TRANSFORMATION: Finalize");
        prompt.Should().Contain("verify");
    }

    [Fact]
    public void GetSystemPrompt_ContainsBartlebyIdentity()
    {
        // Act
        var prompt = _sut.GetSystemPrompt(TransformationType.Execute, "/work");

        // Assert
        prompt.Should().Contain("Bartleby");
        prompt.Should().Contain("provenance");
        prompt.Should().Contain("parsimony");
    }

    #endregion

    #region BuildUserPrompt Tests

    [Fact]
    public void BuildUserPrompt_WithNullContext_ThrowsArgumentNullException()
    {
        var act = () => _sut.BuildUserPrompt(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildUserPrompt_IncludesWorkItemTitle()
    {
        // Arrange
        var context = CreateContext("Test Feature Implementation");

        // Act
        var prompt = _sut.BuildUserPrompt(context);

        // Assert
        prompt.Should().Contain("Test Feature Implementation");
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
        prompt.Should().Contain("Implement the new authentication feature");
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
        prompt.Should().Contain("feature");
        prompt.Should().Contain("auth");
        prompt.Should().Contain("priority-high");
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
        prompt.Should().Contain("https://github.com/test/repo/issues/123");
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
        prompt.Should().Contain("Previous Work Sessions");
        prompt.Should().Contain("Plan");
        prompt.Should().Contain("Completed");
        prompt.Should().Contain("Created implementation plan");
        prompt.Should().Contain("PLAN.md");
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
        prompt.Should().Contain("Answered Questions");
        prompt.Should().Contain("Which database should we use?");
        prompt.Should().Contain("PostgreSQL");
        prompt.Should().Contain("Should we use ORM?");
        prompt.Should().Contain("Yes, use Entity Framework");
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
        prompt.Should().Contain("Additional Instructions");
        prompt.Should().Contain("Focus on performance optimization");
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
        prompt.Should().NotContain("Labels:");
        prompt.Should().NotContain("Previous Work Sessions");
        prompt.Should().NotContain("Answered Questions");
        prompt.Should().NotContain("Additional Instructions");
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

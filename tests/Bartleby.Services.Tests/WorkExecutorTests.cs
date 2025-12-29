using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Bartleby.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Bartleby.Services.Tests;

public class WorkExecutorTests
{
    private readonly Mock<IAIProvider> _aiProviderMock;
    private readonly Mock<IWorkItemRepository> _workItemRepoMock;
    private readonly Mock<IWorkSessionRepository> _sessionRepoMock;
    private readonly Mock<IBlockedQuestionRepository> _questionRepoMock;
    private readonly Mock<ISettingsRepository> _settingsRepoMock;
    private readonly Mock<ILogger<WorkExecutor>> _loggerMock;
    private readonly WorkExecutor _sut;

    public WorkExecutorTests()
    {
        _aiProviderMock = new Mock<IAIProvider>();
        _workItemRepoMock = new Mock<IWorkItemRepository>();
        _sessionRepoMock = new Mock<IWorkSessionRepository>();
        _questionRepoMock = new Mock<IBlockedQuestionRepository>();
        _settingsRepoMock = new Mock<ISettingsRepository>();
        _loggerMock = new Mock<ILogger<WorkExecutor>>();

        _settingsRepoMock
            .Setup(r => r.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppSettings { WorkingDirectory = "/test/work" });

        _sessionRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<WorkSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkSession s, CancellationToken _) => s);

        _workItemRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem w, CancellationToken _) => w);

        _sut = new WorkExecutor(
            _aiProviderMock.Object,
            _workItemRepoMock.Object,
            _sessionRepoMock.Object,
            _questionRepoMock.Object,
            _settingsRepoMock.Object,
            _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullAIProvider_ThrowsArgumentNullException()
    {
        var act = () => new WorkExecutor(
            null!,
            _workItemRepoMock.Object,
            _sessionRepoMock.Object,
            _questionRepoMock.Object,
            _settingsRepoMock.Object,
            _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("aiProvider");
    }

    [Fact]
    public void Constructor_WithNullWorkItemRepository_ThrowsArgumentNullException()
    {
        var act = () => new WorkExecutor(
            _aiProviderMock.Object,
            null!,
            _sessionRepoMock.Object,
            _questionRepoMock.Object,
            _settingsRepoMock.Object,
            _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("workItemRepository");
    }

    [Fact]
    public void Constructor_WithNullSessionRepository_ThrowsArgumentNullException()
    {
        var act = () => new WorkExecutor(
            _aiProviderMock.Object,
            _workItemRepoMock.Object,
            null!,
            _questionRepoMock.Object,
            _settingsRepoMock.Object,
            _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("workSessionRepository");
    }

    [Fact]
    public void Constructor_WithNullQuestionRepository_ThrowsArgumentNullException()
    {
        var act = () => new WorkExecutor(
            _aiProviderMock.Object,
            _workItemRepoMock.Object,
            _sessionRepoMock.Object,
            null!,
            _settingsRepoMock.Object,
            _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("blockedQuestionRepository");
    }

    [Fact]
    public void Constructor_WithNullSettingsRepository_ThrowsArgumentNullException()
    {
        var act = () => new WorkExecutor(
            _aiProviderMock.Object,
            _workItemRepoMock.Object,
            _sessionRepoMock.Object,
            _questionRepoMock.Object,
            null!,
            _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("settingsRepository");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var act = () => new WorkExecutor(
            _aiProviderMock.Object,
            _workItemRepoMock.Object,
            _sessionRepoMock.Object,
            _questionRepoMock.Object,
            _settingsRepoMock.Object,
            null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WithNullContext_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ExecuteAsync(null!));
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ReturnsCompletedResponse()
    {
        // Arrange
        var context = CreateContext();
        var aiResult = new WorkExecutionResult
        {
            Success = true,
            Outcome = WorkExecutionOutcome.Completed,
            Summary = "Work completed successfully",
            ModifiedFiles = ["file1.cs", "file2.cs"],
            TokensUsed = 1500
        };

        _aiProviderMock
            .Setup(p => p.ExecutePromptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(aiResult);

        // Act
        var response = await _sut.ExecuteAsync(context);

        // Assert
        response.Success.Should().BeTrue();
        response.Outcome.Should().Be(WorkExecutionOutcome.Completed);
        response.TransformationType.Should().Be(TransformationType.Execute);
        response.Summary.Should().Be("Work completed successfully");
        response.ModifiedFiles.Should().BeEquivalentTo(["file1.cs", "file2.cs"]);
        response.TokensUsed.Should().Be(1500);
        response.WorkSession.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_CreatesWorkSession()
    {
        // Arrange
        var context = CreateContext();
        WorkSession? capturedSession = null;

        _sessionRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<WorkSession>(), It.IsAny<CancellationToken>()))
            .Callback<WorkSession, CancellationToken>((s, _) => capturedSession = s)
            .ReturnsAsync((WorkSession s, CancellationToken _) => s);

        _aiProviderMock
            .Setup(p => p.ExecutePromptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkExecutionResult
            {
                Success = true,
                Outcome = WorkExecutionOutcome.Completed,
                Summary = "Done",
                TokensUsed = 100
            });

        // Act
        await _sut.ExecuteAsync(context);

        // Assert
        capturedSession.Should().NotBeNull();
        capturedSession!.WorkItemId.Should().Be(context.WorkItem.Id);
        capturedSession.TransformationType.Should().Be(TransformationType.Execute);
        capturedSession.Outcome.Should().Be(WorkSessionOutcome.Completed);
        capturedSession.TokensUsed.Should().Be(100);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBlocked_CreatesBlockedQuestions()
    {
        // Arrange
        var context = CreateContext();
        var aiResult = new WorkExecutionResult
        {
            Success = false,
            Outcome = WorkExecutionOutcome.Blocked,
            Summary = "Need more info",
            Questions = ["What API to use?", "What format?"],
            TokensUsed = 500
        };

        _aiProviderMock
            .Setup(p => p.ExecutePromptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(aiResult);

        _questionRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<BlockedQuestion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlockedQuestion q, CancellationToken _) => q);

        // Act
        var response = await _sut.ExecuteAsync(context);

        // Assert
        response.Success.Should().BeFalse();
        response.Outcome.Should().Be(WorkExecutionOutcome.Blocked);
        response.Questions.Should().HaveCount(2);

        _questionRepoMock.Verify(
            r => r.CreateAsync(It.IsAny<BlockedQuestion>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesWorkItemAttemptCount()
    {
        // Arrange
        var context = CreateContext();
        context.WorkItem.AttemptCount = 2;

        _aiProviderMock
            .Setup(p => p.ExecutePromptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkExecutionResult
            {
                Success = true,
                Outcome = WorkExecutionOutcome.Completed
            });

        // Act
        await _sut.ExecuteAsync(context);

        // Assert
        _workItemRepoMock.Verify(
            r => r.UpdateAsync(
                It.Is<WorkItem>(w => w.AttemptCount == 3 && w.LastWorkedAt != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_OnException_RecordsFailedSession()
    {
        // Arrange
        var context = CreateContext();
        WorkSession? capturedSession = null;

        _aiProviderMock
            .Setup(p => p.ExecutePromptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("AI service unavailable"));

        _sessionRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<WorkSession>(), It.IsAny<CancellationToken>()))
            .Callback<WorkSession, CancellationToken>((s, _) => capturedSession = s)
            .ReturnsAsync((WorkSession s, CancellationToken _) => s);

        // Act
        var response = await _sut.ExecuteAsync(context);

        // Assert
        response.Success.Should().BeFalse();
        response.Outcome.Should().Be(WorkExecutionOutcome.Failed);
        response.ErrorMessage.Should().Contain("AI service unavailable");

        capturedSession.Should().NotBeNull();
        capturedSession!.Outcome.Should().Be(WorkSessionOutcome.Failed);
        capturedSession.ErrorMessage.Should().Contain("AI service unavailable");
    }

    #endregion

    #region BuildContextAsync Tests

    [Fact]
    public async Task BuildContextAsync_WhenWorkItemNotFound_ReturnsNull()
    {
        // Arrange
        _workItemRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem?)null);

        // Act
        var result = await _sut.BuildContextAsync(Guid.NewGuid(), TransformationType.Execute);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task BuildContextAsync_IncludesWorkItem()
    {
        // Arrange
        var workItemId = Guid.NewGuid();
        var workItem = CreateWorkItem(workItemId);

        _workItemRepoMock
            .Setup(r => r.GetByIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        _sessionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _questionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.BuildContextAsync(workItemId, TransformationType.Plan);

        // Assert
        result.Should().NotBeNull();
        result!.WorkItem.Should().Be(workItem);
        result.TransformationType.Should().Be(TransformationType.Plan);
        result.WorkingDirectory.Should().Be("/test/work");
    }

    [Fact]
    public async Task BuildContextAsync_IncludesPreviousSessions()
    {
        // Arrange
        var workItemId = Guid.NewGuid();
        var workItem = CreateWorkItem(workItemId);
        var sessions = new List<WorkSession>
        {
            new() { WorkItemId = workItemId, StartedAt = DateTime.UtcNow.AddHours(-2) },
            new() { WorkItemId = workItemId, StartedAt = DateTime.UtcNow.AddHours(-1) }
        };

        _workItemRepoMock
            .Setup(r => r.GetByIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        _sessionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        _questionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.BuildContextAsync(workItemId, TransformationType.Execute);

        // Assert
        result!.PreviousSessions.Should().HaveCount(2);
        result.PreviousSessions[0].StartedAt.Should().BeBefore(result.PreviousSessions[1].StartedAt);
    }

    [Fact]
    public async Task BuildContextAsync_IncludesOnlyAnsweredQuestions()
    {
        // Arrange
        var workItemId = Guid.NewGuid();
        var workItem = CreateWorkItem(workItemId);
        var questions = new List<BlockedQuestion>
        {
            new() { WorkItemId = workItemId, Question = "Q1", Answer = "A1" },
            new() { WorkItemId = workItemId, Question = "Q2", Answer = null },
            new() { WorkItemId = workItemId, Question = "Q3", Answer = "A3" }
        };

        _workItemRepoMock
            .Setup(r => r.GetByIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        _sessionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _questionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(questions);

        // Act
        var result = await _sut.BuildContextAsync(workItemId, TransformationType.Execute);

        // Assert
        result!.AnsweredQuestions.Should().HaveCount(2);
        result.AnsweredQuestions.Select(q => q.Question).Should().BeEquivalentTo(["Q1", "Q3"]);
    }

    #endregion

    #region GetNextTransformationAsync Tests

    [Fact]
    public async Task GetNextTransformationAsync_WithNoSessions_ReturnsInterpret()
    {
        // Arrange
        var workItemId = Guid.NewGuid();

        _sessionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _questionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.GetNextTransformationAsync(workItemId);

        // Assert
        result.Should().Be(TransformationType.Interpret);
    }

    [Fact]
    public async Task GetNextTransformationAsync_WithUnansweredQuestions_ReturnsAskClarification()
    {
        // Arrange
        var workItemId = Guid.NewGuid();
        var sessions = new List<WorkSession>
        {
            new()
            {
                WorkItemId = workItemId,
                Outcome = WorkSessionOutcome.Blocked,
                TransformationType = TransformationType.Execute
            }
        };
        var questions = new List<BlockedQuestion>
        {
            new() { WorkItemId = workItemId, Question = "Unanswered", Answer = null }
        };

        _sessionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        _questionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(questions);

        // Act
        var result = await _sut.GetNextTransformationAsync(workItemId);

        // Assert
        result.Should().Be(TransformationType.AskClarification);
    }

    [Fact]
    public async Task GetNextTransformationAsync_AfterCompletedInterpret_ReturnsPlan()
    {
        // Arrange
        var workItemId = Guid.NewGuid();
        var sessions = new List<WorkSession>
        {
            new()
            {
                WorkItemId = workItemId,
                Outcome = WorkSessionOutcome.Completed,
                TransformationType = TransformationType.Interpret,
                StartedAt = DateTime.UtcNow
            }
        };

        _sessionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        _questionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.GetNextTransformationAsync(workItemId);

        // Assert
        result.Should().Be(TransformationType.Plan);
    }

    [Fact]
    public async Task GetNextTransformationAsync_AfterCompletedPlan_ReturnsExecute()
    {
        // Arrange
        var workItemId = Guid.NewGuid();
        var sessions = new List<WorkSession>
        {
            new()
            {
                WorkItemId = workItemId,
                Outcome = WorkSessionOutcome.Completed,
                TransformationType = TransformationType.Plan,
                StartedAt = DateTime.UtcNow
            }
        };

        _sessionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        _questionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.GetNextTransformationAsync(workItemId);

        // Assert
        result.Should().Be(TransformationType.Execute);
    }

    [Fact]
    public async Task GetNextTransformationAsync_AfterFailed_ReturnsRefine()
    {
        // Arrange
        var workItemId = Guid.NewGuid();
        var sessions = new List<WorkSession>
        {
            new()
            {
                WorkItemId = workItemId,
                Outcome = WorkSessionOutcome.Failed,
                TransformationType = TransformationType.Execute,
                StartedAt = DateTime.UtcNow
            }
        };

        _sessionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        _questionRepoMock
            .Setup(r => r.GetByWorkItemIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.GetNextTransformationAsync(workItemId);

        // Assert
        result.Should().Be(TransformationType.Refine);
    }

    #endregion

    #region Helper Methods

    private static WorkExecutionContext CreateContext()
    {
        return new WorkExecutionContext
        {
            WorkItem = CreateWorkItem(Guid.NewGuid()),
            TransformationType = TransformationType.Execute,
            WorkingDirectory = "/test/work"
        };
    }

    private static WorkItem CreateWorkItem(Guid id)
    {
        return new WorkItem
        {
            Id = id,
            Title = "Test Work Item",
            Description = "A test work item for unit testing",
            Labels = ["test", "unit-test"]
        };
    }

    #endregion
}

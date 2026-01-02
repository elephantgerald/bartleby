using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Bartleby.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Bartleby.Services.Tests;

public class OrchestratorServiceTests : IDisposable
{
    private readonly Mock<IDependencyResolver> _dependencyResolverMock;
    private readonly Mock<IWorkExecutor> _workExecutorMock;
    private readonly Mock<IWorkItemRepository> _workItemRepoMock;
    private readonly Mock<ISettingsRepository> _settingsRepoMock;
    private readonly Mock<ILogger<OrchestratorService>> _loggerMock;
    private readonly FakeTimeProvider _timeProvider;
    private OrchestratorService _sut;
    private AppSettings _settings;

    public OrchestratorServiceTests()
    {
        _dependencyResolverMock = new Mock<IDependencyResolver>();
        _workExecutorMock = new Mock<IWorkExecutor>();
        _workItemRepoMock = new Mock<IWorkItemRepository>();
        _settingsRepoMock = new Mock<ISettingsRepository>();
        _loggerMock = new Mock<ILogger<OrchestratorService>>();
        _timeProvider = new FakeTimeProvider(new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc));

        _settings = CreateDefaultSettings();

        _settingsRepoMock
            .Setup(r => r.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _settings);

        _settingsRepoMock
            .Setup(r => r.SaveSettingsAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _workItemRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem w, CancellationToken _) => w);

        _dependencyResolverMock
            .Setup(r => r.GetReadyWorkItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        _sut = CreateService();
    }

    private OrchestratorService CreateService()
    {
        return new OrchestratorService(
            _dependencyResolverMock.Object,
            _workExecutorMock.Object,
            _workItemRepoMock.Object,
            _settingsRepoMock.Object,
            _loggerMock.Object,
            _timeProvider);
    }

    private static AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            OrchestratorEnabled = true,
            OrchestratorIntervalMinutes = 5,
            MaxConcurrentWorkItems = 1,
            MaxRetryAttempts = 3,
            QuietHoursEnabled = false,
            QuietHoursStart = new TimeOnly(22, 0),
            QuietHoursEnd = new TimeOnly(7, 0),
            TokenBudgetEnabled = false,
            DailyTokenBudget = 100000,
            TokensUsedToday = 0,
            TokensLastResetDate = DateTime.UtcNow.Date
        };
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullDependencyResolver_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new OrchestratorService(
            null!,
            _workExecutorMock.Object,
            _workItemRepoMock.Object,
            _settingsRepoMock.Object,
            _loggerMock.Object,
            _timeProvider));

        Assert.Equal("dependencyResolver", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullWorkExecutor_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new OrchestratorService(
            _dependencyResolverMock.Object,
            null!,
            _workItemRepoMock.Object,
            _settingsRepoMock.Object,
            _loggerMock.Object,
            _timeProvider));

        Assert.Equal("workExecutor", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullWorkItemRepository_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new OrchestratorService(
            _dependencyResolverMock.Object,
            _workExecutorMock.Object,
            null!,
            _settingsRepoMock.Object,
            _loggerMock.Object,
            _timeProvider));

        Assert.Equal("workItemRepository", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSettingsRepository_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new OrchestratorService(
            _dependencyResolverMock.Object,
            _workExecutorMock.Object,
            _workItemRepoMock.Object,
            null!,
            _loggerMock.Object,
            _timeProvider));

        Assert.Equal("settingsRepository", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new OrchestratorService(
            _dependencyResolverMock.Object,
            _workExecutorMock.Object,
            _workItemRepoMock.Object,
            _settingsRepoMock.Object,
            null!,
            _timeProvider));

        Assert.Equal("logger", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_UsesSystemTimeProvider()
    {
        var service = new OrchestratorService(
            _dependencyResolverMock.Object,
            _workExecutorMock.Object,
            _workItemRepoMock.Object,
            _settingsRepoMock.Object,
            _loggerMock.Object,
            null);

        Assert.NotNull(service);
        service.Dispose();
    }

    #endregion

    #region Service Lifecycle Tests

    [Fact]
    public void InitialState_IsStopped()
    {
        Assert.Equal(OrchestratorState.Stopped, _sut.State);
        Assert.False(_sut.IsRunning);
    }

    [Fact]
    public async Task StartAsync_TransitionsToIdle()
    {
        await _sut.StartAsync();

        Assert.True(_sut.IsRunning);
        // State may be Idle or Working depending on timing
        Assert.Contains(_sut.State, new[] { OrchestratorState.Idle, OrchestratorState.Working });
    }

    [Fact]
    public async Task StartAsync_InitializesStats()
    {
        await _sut.StartAsync();

        Assert.Equal(_timeProvider.UtcNow, _sut.Stats.SessionStartedAt);
        Assert.Equal(0, _sut.Stats.WorkItemsCompleted);
        Assert.Equal(0, _sut.Stats.WorkItemsFailed);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_DoesNotRestart()
    {
        await _sut.StartAsync();
        var firstStartTime = _sut.Stats.SessionStartedAt;

        await Task.Delay(50); // Small delay
        await _sut.StartAsync();

        Assert.Equal(firstStartTime, _sut.Stats.SessionStartedAt);
    }

    [Fact]
    public async Task StopAsync_TransitionsToStopped()
    {
        await _sut.StartAsync();
        await _sut.StopAsync();

        Assert.Equal(OrchestratorState.Stopped, _sut.State);
        Assert.False(_sut.IsRunning);
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_DoesNothing()
    {
        // Should not throw
        await _sut.StopAsync();

        Assert.Equal(OrchestratorState.Stopped, _sut.State);
    }

    [Fact]
    public async Task StartAsync_ThenStopAsync_ThenStartAsync_Works()
    {
        await _sut.StartAsync();
        Assert.True(_sut.IsRunning);

        await _sut.StopAsync();
        Assert.False(_sut.IsRunning);

        await _sut.StartAsync();
        Assert.True(_sut.IsRunning);
    }

    #endregion

    #region State Change Event Tests

    [Fact]
    public async Task StartAsync_RaisesStateChangedEvent()
    {
        var stateChanges = new List<OrchestratorStateChangedEventArgs>();
        _sut.StateChanged += (_, e) => stateChanges.Add(e);

        await _sut.StartAsync();

        Assert.Contains(stateChanges, e => e.NewState == OrchestratorState.Starting);
    }

    [Fact]
    public async Task StopAsync_RaisesStateChangedEvent()
    {
        await _sut.StartAsync();

        var stateChanges = new List<OrchestratorStateChangedEventArgs>();
        _sut.StateChanged += (_, e) => stateChanges.Add(e);

        await _sut.StopAsync();

        Assert.Contains(stateChanges, e => e.NewState == OrchestratorState.Stopping);
        Assert.Contains(stateChanges, e => e.NewState == OrchestratorState.Stopped);
    }

    #endregion

    #region Quiet Hours Tests

    [Fact]
    public async Task WorkCycle_DuringQuietHours_SetsQuietHoursState()
    {
        // Set time to 23:00 (within quiet hours 22:00-07:00)
        _timeProvider.SetTime(new DateTime(2025, 1, 15, 23, 0, 0, DateTimeKind.Local));

        _settings.QuietHoursEnabled = true;
        _settings.QuietHoursStart = new TimeOnly(22, 0);
        _settings.QuietHoursEnd = new TimeOnly(7, 0);

        await _sut.StartAsync();
        await Task.Delay(100); // Allow work cycle to run

        Assert.Equal(OrchestratorState.QuietHours, _sut.State);
    }

    [Fact]
    public async Task WorkCycle_OutsideQuietHours_DoesNotSetQuietHoursState()
    {
        // Set time to 14:00 (outside quiet hours 22:00-07:00)
        _timeProvider.SetTime(new DateTime(2025, 1, 15, 14, 0, 0, DateTimeKind.Local));

        _settings.QuietHoursEnabled = true;
        _settings.QuietHoursStart = new TimeOnly(22, 0);
        _settings.QuietHoursEnd = new TimeOnly(7, 0);

        await _sut.StartAsync();
        await Task.Delay(100); // Allow work cycle to run

        Assert.NotEqual(OrchestratorState.QuietHours, _sut.State);
    }

    [Fact]
    public async Task WorkCycle_QuietHoursDisabled_IgnoresQuietHours()
    {
        // Set time to 23:00 (would be quiet hours if enabled)
        _timeProvider.SetTime(new DateTime(2025, 1, 15, 23, 0, 0, DateTimeKind.Local));

        _settings.QuietHoursEnabled = false;
        _settings.QuietHoursStart = new TimeOnly(22, 0);
        _settings.QuietHoursEnd = new TimeOnly(7, 0);

        await _sut.StartAsync();
        await Task.Delay(100); // Allow work cycle to run

        Assert.NotEqual(OrchestratorState.QuietHours, _sut.State);
    }

    [Theory]
    [InlineData(22, 0, true)]  // Exactly at start
    [InlineData(23, 0, true)]  // During quiet hours
    [InlineData(0, 0, true)]   // Midnight (overnight)
    [InlineData(6, 59, true)]  // Just before end
    [InlineData(7, 0, false)]  // Exactly at end
    [InlineData(14, 0, false)] // Afternoon
    [InlineData(21, 59, false)] // Just before start
    public async Task QuietHours_OvernightRange_CalculatesCorrectly(int hour, int minute, bool expectedInQuietHours)
    {
        _timeProvider.SetTime(new DateTime(2025, 1, 15, hour, minute, 0, DateTimeKind.Local));

        _settings.QuietHoursEnabled = true;
        _settings.QuietHoursStart = new TimeOnly(22, 0);
        _settings.QuietHoursEnd = new TimeOnly(7, 0);

        await _sut.StartAsync();
        await Task.Delay(100);

        if (expectedInQuietHours)
        {
            Assert.Equal(OrchestratorState.QuietHours, _sut.State);
        }
        else
        {
            Assert.NotEqual(OrchestratorState.QuietHours, _sut.State);
        }
    }

    [Theory]
    [InlineData(9, 0, true)]   // Exactly at start
    [InlineData(12, 0, true)]  // During quiet hours
    [InlineData(16, 59, true)] // Just before end
    [InlineData(17, 0, false)] // Exactly at end
    [InlineData(8, 0, false)]  // Before start
    [InlineData(20, 0, false)] // After end
    public async Task QuietHours_SameDayRange_CalculatesCorrectly(int hour, int minute, bool expectedInQuietHours)
    {
        _timeProvider.SetTime(new DateTime(2025, 1, 15, hour, minute, 0, DateTimeKind.Local));

        _settings.QuietHoursEnabled = true;
        _settings.QuietHoursStart = new TimeOnly(9, 0);
        _settings.QuietHoursEnd = new TimeOnly(17, 0);

        await _sut.StartAsync();
        await Task.Delay(100);

        if (expectedInQuietHours)
        {
            Assert.Equal(OrchestratorState.QuietHours, _sut.State);
        }
        else
        {
            Assert.NotEqual(OrchestratorState.QuietHours, _sut.State);
        }
    }

    #endregion

    #region Token Budget Tests

    [Fact]
    public async Task WorkCycle_WhenBudgetExhausted_SetsBudgetExhaustedState()
    {
        _settings.TokenBudgetEnabled = true;
        _settings.DailyTokenBudget = 1000;
        _settings.TokensUsedToday = 1000;

        await _sut.StartAsync();
        await Task.Delay(100);

        Assert.Equal(OrchestratorState.BudgetExhausted, _sut.State);
    }

    [Fact]
    public async Task WorkCycle_WhenBudgetNotExhausted_DoesNotSetBudgetExhaustedState()
    {
        _settings.TokenBudgetEnabled = true;
        _settings.DailyTokenBudget = 1000;
        _settings.TokensUsedToday = 500;

        await _sut.StartAsync();
        await Task.Delay(100);

        Assert.NotEqual(OrchestratorState.BudgetExhausted, _sut.State);
    }

    [Fact]
    public async Task WorkCycle_WhenBudgetDisabled_IgnoresBudget()
    {
        _settings.TokenBudgetEnabled = false;
        _settings.DailyTokenBudget = 1000;
        _settings.TokensUsedToday = 2000; // Over budget but disabled

        await _sut.StartAsync();
        await Task.Delay(100);

        Assert.NotEqual(OrchestratorState.BudgetExhausted, _sut.State);
    }

    [Fact]
    public async Task WorkCycle_ResetsDailyTokensAtMidnight()
    {
        _settings.TokenBudgetEnabled = true;
        _settings.TokensUsedToday = 5000;
        _settings.TokensLastResetDate = _timeProvider.UtcNow.Date.AddDays(-1); // Yesterday

        await _sut.StartAsync();
        await Task.Delay(100);

        _settingsRepoMock.Verify(
            r => r.SaveSettingsAsync(
                It.Is<AppSettings>(s => s.TokensUsedToday == 0),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WorkCycle_UpdatesTokenUsageAfterExecution()
    {
        var workItem = CreateWorkItem();
        _dependencyResolverMock
            .Setup(r => r.GetReadyWorkItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { workItem });

        _workExecutorMock
            .Setup(e => e.GetNextTransformationAsync(workItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransformationType.Execute);

        _workExecutorMock
            .Setup(e => e.BuildContextAsync(workItem.Id, TransformationType.Execute, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkExecutionContext
            {
                WorkItem = workItem,
                TransformationType = TransformationType.Execute,
                WorkingDirectory = "/test"
            });

        _workExecutorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<WorkExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkExecutionResponse
            {
                Success = true,
                Outcome = WorkExecutionOutcome.Completed,
                TransformationType = TransformationType.Execute,
                TokensUsed = 500
            });

        _workItemRepoMock
            .Setup(r => r.GetByIdAsync(workItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        await _sut.StartAsync();
        await Task.Delay(200); // Allow work cycle to complete

        Assert.True(_sut.Stats.TokensUsedThisSession >= 500);
    }

    #endregion

    #region Work Item Processing Tests

    [Fact]
    public async Task WorkCycle_WithNoReadyItems_RemainsIdle()
    {
        _dependencyResolverMock
            .Setup(r => r.GetReadyWorkItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        await _sut.StartAsync();
        await Task.Delay(100);

        Assert.Equal(OrchestratorState.Idle, _sut.State);
    }

    [Fact]
    public async Task WorkCycle_WithReadyItem_ProcessesItem()
    {
        var workItem = CreateWorkItem();
        _dependencyResolverMock
            .Setup(r => r.GetReadyWorkItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { workItem });

        _workExecutorMock
            .Setup(e => e.GetNextTransformationAsync(workItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransformationType.Execute);

        _workExecutorMock
            .Setup(e => e.BuildContextAsync(workItem.Id, TransformationType.Execute, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkExecutionContext
            {
                WorkItem = workItem,
                TransformationType = TransformationType.Execute,
                WorkingDirectory = "/test"
            });

        _workExecutorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<WorkExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkExecutionResponse
            {
                Success = true,
                Outcome = WorkExecutionOutcome.Completed,
                TransformationType = TransformationType.Finalize,
                TokensUsed = 100
            });

        _workItemRepoMock
            .Setup(r => r.GetByIdAsync(workItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        await _sut.StartAsync();
        await Task.Delay(200);

        _workExecutorMock.Verify(
            e => e.ExecuteAsync(It.IsAny<WorkExecutionContext>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WorkCycle_RaisesWorkItemStatusChangedEvent()
    {
        var workItem = CreateWorkItem();
        _dependencyResolverMock
            .Setup(r => r.GetReadyWorkItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { workItem });

        _workExecutorMock
            .Setup(e => e.GetNextTransformationAsync(workItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransformationType.Execute);

        _workExecutorMock
            .Setup(e => e.BuildContextAsync(workItem.Id, TransformationType.Execute, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkExecutionContext
            {
                WorkItem = workItem,
                TransformationType = TransformationType.Execute,
                WorkingDirectory = "/test"
            });

        _workExecutorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<WorkExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkExecutionResponse
            {
                Success = true,
                Outcome = WorkExecutionOutcome.Completed,
                TransformationType = TransformationType.Finalize,
                TokensUsed = 100
            });

        _workItemRepoMock
            .Setup(r => r.GetByIdAsync(workItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        var statusChanges = new List<WorkItemStatusChangedEventArgs>();
        _sut.WorkItemStatusChanged += (_, e) => statusChanges.Add(e);

        await _sut.StartAsync();
        await Task.Delay(200);

        Assert.Contains(statusChanges, e => e.NewStatus == WorkItemStatus.InProgress);
    }

    #endregion

    #region State Machine Transition Tests

    [Fact]
    public async Task ProcessWorkItem_OnCompletion_TransitionsToComplete()
    {
        var workItem = CreateWorkItem();
        WorkItemStatus? finalStatus = null;

        _dependencyResolverMock
            .Setup(r => r.GetReadyWorkItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { workItem });

        _workExecutorMock
            .Setup(e => e.GetNextTransformationAsync(workItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransformationType.Finalize);

        _workExecutorMock
            .Setup(e => e.BuildContextAsync(workItem.Id, TransformationType.Finalize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkExecutionContext
            {
                WorkItem = workItem,
                TransformationType = TransformationType.Finalize,
                WorkingDirectory = "/test"
            });

        _workExecutorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<WorkExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkExecutionResponse
            {
                Success = true,
                Outcome = WorkExecutionOutcome.Completed,
                TransformationType = TransformationType.Finalize,
                TokensUsed = 100
            });

        _workItemRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .Callback<WorkItem, CancellationToken>((w, _) => finalStatus = w.Status)
            .ReturnsAsync((WorkItem w, CancellationToken _) => w);

        _workItemRepoMock
            .Setup(r => r.GetByIdAsync(workItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        await _sut.StartAsync();
        await Task.Delay(200);

        Assert.Equal(WorkItemStatus.Complete, finalStatus);
    }

    [Fact]
    public async Task ProcessWorkItem_OnBlocked_TransitionsToBlocked()
    {
        var workItem = CreateWorkItem();
        WorkItemStatus? finalStatus = null;

        _dependencyResolverMock
            .Setup(r => r.GetReadyWorkItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { workItem });

        _workExecutorMock
            .Setup(e => e.GetNextTransformationAsync(workItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransformationType.Execute);

        _workExecutorMock
            .Setup(e => e.BuildContextAsync(workItem.Id, TransformationType.Execute, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkExecutionContext
            {
                WorkItem = workItem,
                TransformationType = TransformationType.Execute,
                WorkingDirectory = "/test"
            });

        _workExecutorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<WorkExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkExecutionResponse
            {
                Success = false,
                Outcome = WorkExecutionOutcome.Blocked,
                TransformationType = TransformationType.Execute,
                Summary = "Need clarification",
                TokensUsed = 50
            });

        _workItemRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .Callback<WorkItem, CancellationToken>((w, _) => finalStatus = w.Status)
            .ReturnsAsync((WorkItem w, CancellationToken _) => w);

        _workItemRepoMock
            .Setup(r => r.GetByIdAsync(workItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        await _sut.StartAsync();
        await Task.Delay(200);

        Assert.Equal(WorkItemStatus.Blocked, finalStatus);
    }

    [Fact]
    public async Task ProcessWorkItem_OnFailure_ReturnsToReadyForRetry()
    {
        var workItem = CreateWorkItem();
        workItem.AttemptCount = 0;
        WorkItemStatus? finalStatus = null;

        _dependencyResolverMock
            .Setup(r => r.GetReadyWorkItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { workItem });

        _workExecutorMock
            .Setup(e => e.GetNextTransformationAsync(workItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransformationType.Execute);

        _workExecutorMock
            .Setup(e => e.BuildContextAsync(workItem.Id, TransformationType.Execute, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkExecutionContext
            {
                WorkItem = workItem,
                TransformationType = TransformationType.Execute,
                WorkingDirectory = "/test"
            });

        _workExecutorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<WorkExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkExecutionResponse
            {
                Success = false,
                Outcome = WorkExecutionOutcome.Failed,
                TransformationType = TransformationType.Execute,
                ErrorMessage = "Something went wrong",
                TokensUsed = 25
            });

        _workItemRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .Callback<WorkItem, CancellationToken>((w, _) => finalStatus = w.Status)
            .ReturnsAsync((WorkItem w, CancellationToken _) => w);

        // Return the work item with AttemptCount still under max
        _workItemRepoMock
            .Setup(r => r.GetByIdAsync(workItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItem { Id = workItem.Id, AttemptCount = 1, Status = WorkItemStatus.InProgress });

        await _sut.StartAsync();
        await Task.Delay(200);

        // Should return to Ready for retry since AttemptCount < MaxRetryAttempts
        Assert.Equal(WorkItemStatus.Ready, finalStatus);
    }

    [Fact]
    public async Task ProcessWorkItem_ExceedsMaxRetries_TransitionsToFailed()
    {
        var workItem = CreateWorkItem();
        workItem.AttemptCount = 3; // Already at max
        _settings.MaxRetryAttempts = 3;

        WorkItemStatus? finalStatus = null;

        _dependencyResolverMock
            .Setup(r => r.GetReadyWorkItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { workItem });

        _workItemRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .Callback<WorkItem, CancellationToken>((w, _) => finalStatus = w.Status)
            .ReturnsAsync((WorkItem w, CancellationToken _) => w);

        await _sut.StartAsync();
        await Task.Delay(200);

        Assert.Equal(WorkItemStatus.Failed, finalStatus);
    }

    #endregion

    #region Trigger Tests

    [Fact]
    public async Task TriggerAsync_WhenNotRunning_DoesNothing()
    {
        // Should not throw
        await _sut.TriggerAsync();

        _dependencyResolverMock.Verify(
            r => r.GetReadyWorkItemsAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TriggerAsync_WhenRunning_TriggersImmediateCycle()
    {
        await _sut.StartAsync();

        // Reset the mock to track new calls
        _dependencyResolverMock.Invocations.Clear();

        await _sut.TriggerAsync();
        await Task.Delay(200);

        _dependencyResolverMock.Verify(
            r => r.GetReadyWorkItemsAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Orchestrator Disabled Tests

    [Fact]
    public async Task WorkCycle_WhenOrchestratorDisabled_DoesNotProcessWork()
    {
        _settings.OrchestratorEnabled = false;

        var workItem = CreateWorkItem();
        _dependencyResolverMock
            .Setup(r => r.GetReadyWorkItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { workItem });

        await _sut.StartAsync();
        await Task.Delay(200);

        _workExecutorMock.Verify(
            e => e.ExecuteAsync(It.IsAny<WorkExecutionContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Helper Methods

    private static WorkItem CreateWorkItem(Guid? id = null)
    {
        return new WorkItem
        {
            Id = id ?? Guid.NewGuid(),
            Title = "Test Work Item",
            Description = "A test work item for unit testing",
            Status = WorkItemStatus.Ready,
            Labels = ["test"]
        };
    }

    #endregion
}

/// <summary>
/// Fake time provider for testing time-sensitive logic.
/// </summary>
public class FakeTimeProvider : ITimeProvider
{
    private DateTime _utcNow;
    private DateTime _localNow;

    public FakeTimeProvider(DateTime utcNow)
    {
        _utcNow = utcNow;
        _localNow = utcNow.ToLocalTime();
    }

    public DateTime UtcNow => _utcNow;
    public DateTime LocalNow => _localNow;

    public void SetTime(DateTime time)
    {
        if (time.Kind == DateTimeKind.Utc)
        {
            _utcNow = time;
            _localNow = time.ToLocalTime();
        }
        else
        {
            _localNow = time;
            _utcNow = time.ToUniversalTime();
        }
    }

    public void AdvanceBy(TimeSpan duration)
    {
        _utcNow = _utcNow.Add(duration);
        _localNow = _localNow.Add(duration);
    }
}

using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Microsoft.Extensions.Logging;

namespace Bartleby.Services;

/// <summary>
/// Background service that orchestrates autonomous work execution.
/// </summary>
/// <remarks>
/// <para>
/// The OrchestratorService coordinates:
/// <list type="bullet">
/// <item>DependencyResolver - to find ready work items</item>
/// <item>WorkExecutor - to execute work through AI</item>
/// <item>Repository updates - to track status changes</item>
/// </list>
/// </para>
/// <para>
/// State Machine:
/// <code>
/// Pending/Ready → InProgress → Complete/Blocked/Failed
/// </code>
/// </para>
/// </remarks>
public class OrchestratorService : IOrchestratorService, IDisposable
{
    private readonly IDependencyResolver _dependencyResolver;
    private readonly IWorkExecutor _workExecutor;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<OrchestratorService> _logger;
    private readonly ITimeProvider _timeProvider;

    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _workSemaphore = new(1, 1);

    private OrchestratorState _state = OrchestratorState.Stopped;
    private OrchestratorStats _stats = new();
    private CancellationTokenSource? _serviceCts;
    private Task? _workLoopTask;
    private Timer? _timer;
    private bool _disposed;

    public OrchestratorService(
        IDependencyResolver dependencyResolver,
        IWorkExecutor workExecutor,
        IWorkItemRepository workItemRepository,
        ISettingsRepository settingsRepository,
        ILogger<OrchestratorService> logger,
        ITimeProvider? timeProvider = null)
    {
        _dependencyResolver = dependencyResolver ?? throw new ArgumentNullException(nameof(dependencyResolver));
        _workExecutor = workExecutor ?? throw new ArgumentNullException(nameof(workExecutor));
        _workItemRepository = workItemRepository ?? throw new ArgumentNullException(nameof(workItemRepository));
        _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? new SystemTimeProvider();
    }

    public bool IsRunning => _state != OrchestratorState.Stopped && _state != OrchestratorState.Stopping;

    public OrchestratorState State
    {
        get { lock (_stateLock) { return _state; } }
    }

    public OrchestratorStats Stats
    {
        get { lock (_stateLock) { return _stats; } }
    }

    public event EventHandler<OrchestratorStateChangedEventArgs>? StateChanged;
    public event EventHandler<WorkItemStatusChangedEventArgs>? WorkItemStatusChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            _logger.LogWarning("Orchestrator is already running");
            return;
        }

        _logger.LogInformation("Starting orchestrator service");
        SetState(OrchestratorState.Starting);

        try
        {
            _serviceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Initialize stats
            lock (_stateLock)
            {
                _stats = new OrchestratorStats
                {
                    SessionStartedAt = _timeProvider.UtcNow
                };
            }

            var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);

            // Reset daily tokens if needed
            await ResetDailyTokensIfNeededAsync(settings, cancellationToken);

            // Start the timer
            var interval = TimeSpan.FromMinutes(Math.Max(1, settings.OrchestratorIntervalMinutes));
            _timer = new Timer(
                OnTimerElapsed,
                null,
                TimeSpan.Zero, // Fire immediately
                interval);

            SetState(OrchestratorState.Idle);
            _logger.LogInformation("Orchestrator service started with interval {Interval}", interval);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start orchestrator service");
            SetState(OrchestratorState.Stopped);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
        {
            _logger.LogDebug("Orchestrator is not running");
            return;
        }

        _logger.LogInformation("Stopping orchestrator service");
        SetState(OrchestratorState.Stopping);

        try
        {
            // Stop the timer
            if (_timer != null)
            {
                await _timer.DisposeAsync();
                _timer = null;
            }

            // Cancel the service
            _serviceCts?.Cancel();

            // Wait for current work to complete (with timeout)
            if (_workLoopTask != null)
            {
                try
                {
                    await _workLoopTask.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Work loop did not complete within timeout");
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            _serviceCts?.Dispose();
            _serviceCts = null;
            _workLoopTask = null;

            _logger.LogInformation("Orchestrator service stopped");
        }
        finally
        {
            SetState(OrchestratorState.Stopped);
        }
    }

    public async Task TriggerAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
        {
            _logger.LogWarning("Cannot trigger - orchestrator is not running");
            return;
        }

        _logger.LogDebug("Manual trigger requested");

        // Reset the timer to fire immediately
        _timer?.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);

        // Wait a moment for the work cycle to start
        await Task.Delay(100, cancellationToken);
    }

    private void OnTimerElapsed(object? state)
    {
        // Avoid overlapping executions
        if (!_workSemaphore.Wait(0))
        {
            _logger.LogDebug("Work cycle already in progress, skipping");
            return;
        }

        try
        {
            _workLoopTask = ExecuteWorkCycleAsync(_serviceCts?.Token ?? CancellationToken.None);
        }
        finally
        {
            // Note: Semaphore is released in ExecuteWorkCycleAsync
        }
    }

    private async Task ExecuteWorkCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);

            // Check if orchestrator is enabled
            if (!settings.OrchestratorEnabled)
            {
                _logger.LogDebug("Orchestrator is disabled in settings");
                return;
            }

            // Reset daily tokens if needed
            await ResetDailyTokensIfNeededAsync(settings, cancellationToken);

            // Check quiet hours
            if (IsInQuietHours(settings))
            {
                SetState(OrchestratorState.QuietHours);
                _logger.LogDebug("In quiet hours, skipping work cycle");
                return;
            }

            // Check token budget
            if (IsBudgetExhausted(settings))
            {
                SetState(OrchestratorState.BudgetExhausted);
                _logger.LogDebug("Token budget exhausted, skipping work cycle");
                return;
            }

            // Get ready work items
            var readyItems = await _dependencyResolver.GetReadyWorkItemsAsync(cancellationToken);

            if (readyItems.Count == 0)
            {
                SetState(OrchestratorState.Idle);
                _logger.LogDebug("No ready work items found");
                return;
            }

            _logger.LogInformation("Found {Count} ready work items", readyItems.Count);

            // Process work items (respecting MaxConcurrentWorkItems = 1 for now)
            foreach (var workItem in readyItems.Take(settings.MaxConcurrentWorkItems))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Re-check budget before each item
                settings = await _settingsRepository.GetSettingsAsync(cancellationToken);
                if (IsBudgetExhausted(settings))
                {
                    SetState(OrchestratorState.BudgetExhausted);
                    _logger.LogInformation("Token budget exhausted, stopping work cycle");
                    break;
                }

                await ProcessWorkItemAsync(workItem, settings, cancellationToken);
            }

            SetState(OrchestratorState.Idle);

            // Update next cycle time
            lock (_stateLock)
            {
                _stats.NextCycleAt = _timeProvider.UtcNow.AddMinutes(settings.OrchestratorIntervalMinutes);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Work cycle cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in work cycle");
            SetState(OrchestratorState.Idle);
        }
        finally
        {
            _workSemaphore.Release();

            // Restore timer interval
            var settings = await _settingsRepository.GetSettingsAsync(CancellationToken.None);
            var interval = TimeSpan.FromMinutes(Math.Max(1, settings.OrchestratorIntervalMinutes));
            _timer?.Change(interval, interval);
        }
    }

    private async Task ProcessWorkItemAsync(WorkItem workItem, AppSettings settings, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing work item {Id}: {Title}", workItem.Id, workItem.Title);

        SetState(OrchestratorState.Working);
        lock (_stateLock)
        {
            _stats.CurrentWorkItemId = workItem.Id;
        }

        var previousStatus = workItem.Status;

        try
        {
            // Check retry limit
            if (workItem.AttemptCount >= settings.MaxRetryAttempts)
            {
                _logger.LogWarning(
                    "Work item {Id} has exceeded max retry attempts ({Attempts}/{Max})",
                    workItem.Id, workItem.AttemptCount, settings.MaxRetryAttempts);

                await UpdateWorkItemStatusAsync(
                    workItem,
                    WorkItemStatus.Failed,
                    "Exceeded maximum retry attempts",
                    cancellationToken);
                return;
            }

            // Transition to InProgress
            await UpdateWorkItemStatusAsync(workItem, WorkItemStatus.InProgress, null, cancellationToken);

            // Get the next transformation type
            var transformationType = await _workExecutor.GetNextTransformationAsync(workItem.Id, cancellationToken);

            // Build context
            var context = await _workExecutor.BuildContextAsync(workItem.Id, transformationType, cancellationToken);
            if (context == null)
            {
                _logger.LogWarning("Failed to build context for work item {Id}", workItem.Id);
                await UpdateWorkItemStatusAsync(
                    workItem,
                    WorkItemStatus.Failed,
                    "Failed to build execution context",
                    cancellationToken);
                return;
            }

            // Execute work
            var response = await _workExecutor.ExecuteAsync(context, cancellationToken);

            // Update token usage
            await UpdateTokenUsageAsync(response.TokensUsed, cancellationToken);

            // Update stats
            lock (_stateLock)
            {
                _stats.TokensUsedThisSession += response.TokensUsed;
            }

            // Handle outcome
            switch (response.Outcome)
            {
                case WorkExecutionOutcome.Completed:
                    // Check if we've completed all transformations (Finalize was successful)
                    if (transformationType == TransformationType.Finalize)
                    {
                        await UpdateWorkItemStatusAsync(
                            workItem,
                            WorkItemStatus.Complete,
                            response.Summary,
                            cancellationToken);

                        lock (_stateLock)
                        {
                            _stats.WorkItemsCompleted++;
                        }
                    }
                    else
                    {
                        // More transformations needed - return to Ready
                        await UpdateWorkItemStatusAsync(
                            workItem,
                            WorkItemStatus.Ready,
                            $"Completed {transformationType}, ready for next step",
                            cancellationToken);
                    }
                    break;

                case WorkExecutionOutcome.Blocked:
                case WorkExecutionOutcome.NeedsMoreContext:
                    await UpdateWorkItemStatusAsync(
                        workItem,
                        WorkItemStatus.Blocked,
                        response.Summary ?? "Blocked waiting for answers",
                        cancellationToken);

                    lock (_stateLock)
                    {
                        _stats.WorkItemsBlocked++;
                    }
                    break;

                case WorkExecutionOutcome.Failed:
                    // Keep as InProgress to allow retry (unless max attempts reached)
                    var updatedItem = await _workItemRepository.GetByIdAsync(workItem.Id, cancellationToken);
                    if (updatedItem != null && updatedItem.AttemptCount >= settings.MaxRetryAttempts)
                    {
                        await UpdateWorkItemStatusAsync(
                            workItem,
                            WorkItemStatus.Failed,
                            response.ErrorMessage ?? "Execution failed",
                            cancellationToken);

                        lock (_stateLock)
                        {
                            _stats.WorkItemsFailed++;
                        }
                    }
                    else
                    {
                        // Return to Ready for retry
                        await UpdateWorkItemStatusAsync(
                            workItem,
                            WorkItemStatus.Ready,
                            $"Attempt failed, will retry: {response.ErrorMessage}",
                            cancellationToken);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing work item {Id}", workItem.Id);

            await UpdateWorkItemStatusAsync(
                workItem,
                WorkItemStatus.Ready,
                $"Processing error: {ex.Message}",
                cancellationToken);
        }
        finally
        {
            lock (_stateLock)
            {
                _stats.CurrentWorkItemId = null;
            }
        }
    }

    private async Task UpdateWorkItemStatusAsync(
        WorkItem workItem,
        WorkItemStatus newStatus,
        string? message,
        CancellationToken cancellationToken)
    {
        var previousStatus = workItem.Status;

        if (previousStatus == newStatus)
        {
            return;
        }

        // Track previous status when transitioning to Blocked
        if (newStatus == WorkItemStatus.Blocked && previousStatus != WorkItemStatus.Blocked)
        {
            workItem.PreviousStatus = previousStatus;
        }

        workItem.Status = newStatus;
        workItem.UpdatedAt = _timeProvider.UtcNow;

        if (newStatus == WorkItemStatus.Failed && message != null)
        {
            workItem.ErrorMessage = message;
        }

        await _workItemRepository.UpdateAsync(workItem, cancellationToken);

        _logger.LogInformation(
            "Work item {Id} status changed: {Previous} → {New}",
            workItem.Id, previousStatus, newStatus);

        OnWorkItemStatusChanged(new WorkItemStatusChangedEventArgs
        {
            WorkItemId = workItem.Id,
            Title = workItem.Title,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Message = message
        });
    }

    private bool IsInQuietHours(AppSettings settings)
    {
        if (!settings.QuietHoursEnabled)
        {
            return false;
        }

        var now = TimeOnly.FromDateTime(_timeProvider.LocalNow);
        var start = settings.QuietHoursStart;
        var end = settings.QuietHoursEnd;

        // Handle overnight quiet hours (e.g., 22:00 to 07:00)
        if (start > end)
        {
            // Overnight: quiet if now >= start OR now < end
            return now >= start || now < end;
        }
        else
        {
            // Same day: quiet if now >= start AND now < end
            return now >= start && now < end;
        }
    }

    private bool IsBudgetExhausted(AppSettings settings)
    {
        if (!settings.TokenBudgetEnabled || settings.DailyTokenBudget <= 0)
        {
            return false;
        }

        return settings.TokensUsedToday >= settings.DailyTokenBudget;
    }

    private async Task ResetDailyTokensIfNeededAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var today = _timeProvider.UtcNow.Date;

        if (settings.TokensLastResetDate.Date < today)
        {
            _logger.LogInformation("Resetting daily token count");

            settings.TokensUsedToday = 0;
            settings.TokensLastResetDate = today;

            await _settingsRepository.SaveSettingsAsync(settings, cancellationToken);
        }

        lock (_stateLock)
        {
            _stats.TokensUsedToday = settings.TokensUsedToday;
            if (settings.TokenBudgetEnabled && settings.DailyTokenBudget > 0)
            {
                _stats.RemainingBudget = Math.Max(0, settings.DailyTokenBudget - settings.TokensUsedToday);
            }
        }
    }

    private async Task UpdateTokenUsageAsync(int tokensUsed, CancellationToken cancellationToken)
    {
        if (tokensUsed <= 0)
        {
            return;
        }

        var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);
        settings.TokensUsedToday += tokensUsed;

        await _settingsRepository.SaveSettingsAsync(settings, cancellationToken);

        lock (_stateLock)
        {
            _stats.TokensUsedToday = settings.TokensUsedToday;
            if (settings.TokenBudgetEnabled && settings.DailyTokenBudget > 0)
            {
                _stats.RemainingBudget = Math.Max(0, settings.DailyTokenBudget - settings.TokensUsedToday);
            }
        }
    }

    private void SetState(OrchestratorState newState)
    {
        OrchestratorState previousState;
        lock (_stateLock)
        {
            if (_state == newState)
            {
                return;
            }

            previousState = _state;
            _state = newState;
        }

        _logger.LogDebug("Orchestrator state changed: {Previous} → {New}", previousState, newState);

        OnStateChanged(new OrchestratorStateChangedEventArgs
        {
            PreviousState = previousState,
            NewState = newState
        });
    }

    protected virtual void OnStateChanged(OrchestratorStateChangedEventArgs e)
    {
        StateChanged?.Invoke(this, e);
    }

    protected virtual void OnWorkItemStatusChanged(WorkItemStatusChangedEventArgs e)
    {
        WorkItemStatusChanged?.Invoke(this, e);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _timer?.Dispose();
            _serviceCts?.Cancel();
            _serviceCts?.Dispose();
            _workSemaphore.Dispose();
        }

        _disposed = true;
    }
}

/// <summary>
/// Provides the current time. Abstracted for testing.
/// </summary>
public interface ITimeProvider
{
    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets the current local time.
    /// </summary>
    DateTime LocalNow { get; }
}

/// <summary>
/// Default time provider using system time.
/// </summary>
public class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime LocalNow => DateTime.Now;
}

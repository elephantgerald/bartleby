using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Bartleby.Services;
using Moq;

namespace Bartleby.Services.Tests;

public class SyncServiceTests
{
    private readonly Mock<IWorkSource> _workSourceMock;
    private readonly Mock<IWorkItemRepository> _workItemRepositoryMock;
    private readonly SyncService _syncService;

    public SyncServiceTests()
    {
        _workSourceMock = new Mock<IWorkSource>();
        _workSourceMock.Setup(w => w.Name).Returns("TestSource");

        _workItemRepositoryMock = new Mock<IWorkItemRepository>();
        _syncService = new SyncService(_workSourceMock.Object, _workItemRepositoryMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullWorkSource_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new SyncService(null!, _workItemRepositoryMock.Object));
        Assert.Equal("workSource", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new SyncService(_workSourceMock.Object, null!));
        Assert.Equal("workItemRepository", ex.ParamName);
    }

    #endregion

    #region Initial State Tests

    [Fact]
    public void IsSyncing_Initially_IsFalse()
    {
        Assert.False(_syncService.IsSyncing);
    }

    [Fact]
    public void LastSyncTime_Initially_IsNull()
    {
        Assert.Null(_syncService.LastSyncTime);
    }

    #endregion

    #region SyncAsync - New Items Added

    [Fact]
    public async Task SyncAsync_WithNewRemoteItem_AddsToLocalStore()
    {
        // Arrange
        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource");

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _workItemRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.ItemsAdded);
        Assert.Equal(0, result.ItemsUpdated);
        Assert.Equal(0, result.ItemsRemoved);

        _workItemRepositoryMock.Verify(
            r => r.CreateAsync(It.Is<WorkItem>(w => w.ExternalId == "1"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncAsync_WithMultipleNewRemoteItems_AddsAllToLocalStore()
    {
        // Arrange
        var remoteItems = new List<WorkItem>
        {
            CreateWorkItem(externalId: "1", source: "TestSource"),
            CreateWorkItem(externalId: "2", source: "TestSource"),
            CreateWorkItem(externalId: "3", source: "TestSource")
        };

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(remoteItems);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _workItemRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.ItemsAdded);

        _workItemRepositoryMock.Verify(
            r => r.CreateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task SyncAsync_WithRemoteItemWithoutExternalId_SkipsItem()
    {
        // Arrange
        var remoteItem = CreateWorkItem(externalId: null, source: "TestSource");

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.ItemsAdded);

        _workItemRepositoryMock.Verify(
            r => r.CreateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region SyncAsync - Existing Items Updated

    [Fact]
    public async Task SyncAsync_WithMatchingLocalItem_UpdatesLocalItem()
    {
        // Arrange
        var localItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "Old Title");
        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "New Title");

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        _workItemRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.ItemsUpdated);
        Assert.Equal(0, result.ItemsAdded);

        _workItemRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<WorkItem>(w => w.Title == "New Title"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncAsync_WithMatchingLocalItem_PreservesLocalId()
    {
        // Arrange
        var localId = Guid.NewGuid();
        var localItem = CreateWorkItem(id: localId, externalId: "1", source: "TestSource", title: "Old Title");
        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "New Title");

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        _workItemRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        await _syncService.SyncAsync();

        // Assert
        _workItemRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<WorkItem>(w => w.Id == localId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncAsync_WithMatchingLocalItem_PreservesLocalOnlyFields()
    {
        // Arrange
        var localItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "Old Title");
        localItem.LastWorkedAt = DateTime.UtcNow.AddHours(-1);
        localItem.AttemptCount = 3;
        localItem.BranchName = "feature/test";
        localItem.ErrorMessage = "Previous error";
        localItem.Dependencies = [Guid.NewGuid()];

        // Remote has different title to trigger update
        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "New Title");

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        WorkItem? capturedItem = null;
        _workItemRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .Callback<WorkItem, CancellationToken>((item, _) => capturedItem = item)
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        await _syncService.SyncAsync();

        // Assert
        Assert.NotNull(capturedItem);
        Assert.Equal(localItem.LastWorkedAt, capturedItem!.LastWorkedAt);
        Assert.Equal(3, capturedItem.AttemptCount);
        Assert.Equal("feature/test", capturedItem.BranchName);
        Assert.Equal("Previous error", capturedItem.ErrorMessage);
        Assert.Equal(1, capturedItem.Dependencies.Count);
    }

    #endregion

    #region SyncAsync - Items Removed

    [Fact]
    public async Task SyncAsync_WithLocalItemNotInRemote_RemovesLocalItem()
    {
        // Arrange
        var localItem = CreateWorkItem(externalId: "1", source: "TestSource");

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.ItemsRemoved);

        _workItemRepositoryMock.Verify(
            r => r.DeleteAsync(localItem.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncAsync_WithLocalItemFromDifferentSource_DoesNotRemoveIt()
    {
        // Arrange
        var localItem = CreateWorkItem(externalId: "1", source: "OtherSource");

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.Equal(0, result.ItemsRemoved);

        _workItemRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region SyncAsync - Status Conflict Resolution

    [Fact]
    public async Task SyncAsync_LocalItemWithDifferentStatus_PushesStatusToSource()
    {
        // Arrange: Local item is InProgress, remote is Pending
        var localItem = CreateWorkItem(externalId: "1", source: "TestSource", status: WorkItemStatus.InProgress);
        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", status: WorkItemStatus.Pending);

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        _workItemRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.StatusesPushed);

        _workSourceMock.Verify(
            w => w.UpdateStatusAsync(It.Is<WorkItem>(wi => wi.Status == WorkItemStatus.InProgress), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncAsync_LocalItemWithDifferentStatus_PreservesLocalStatus()
    {
        // Arrange: Remote has different title to trigger update, and different status
        var localItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "Old Title", status: WorkItemStatus.InProgress);
        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "New Title", status: WorkItemStatus.Pending);

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        WorkItem? capturedItem = null;
        _workItemRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .Callback<WorkItem, CancellationToken>((item, _) => capturedItem = item)
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        await _syncService.SyncAsync();

        // Assert
        Assert.NotNull(capturedItem);
        Assert.Equal(WorkItemStatus.InProgress, capturedItem!.Status);
    }

    [Fact]
    public async Task SyncAsync_LocalPendingStatusMatchesRemote_DoesNotPushStatus()
    {
        // Arrange: Both are Pending, no need to push
        var localItem = CreateWorkItem(externalId: "1", source: "TestSource", status: WorkItemStatus.Pending);
        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", status: WorkItemStatus.Pending);

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        _workItemRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.Equal(0, result.StatusesPushed);

        _workSourceMock.Verify(
            w => w.UpdateStatusAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncAsync_LocalPendingDiffersFromRemoteReady_DoesNotPushPending()
    {
        // Arrange: Local is Pending (default), Remote is Ready
        // Pending is the default state, so we don't push it back (let source control)
        var localItem = CreateWorkItem(externalId: "1", source: "TestSource", status: WorkItemStatus.Pending);
        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", status: WorkItemStatus.Ready);

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        _workItemRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.Equal(0, result.StatusesPushed);

        _workSourceMock.Verify(
            w => w.UpdateStatusAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(WorkItemStatus.Ready)]
    [InlineData(WorkItemStatus.InProgress)]
    [InlineData(WorkItemStatus.Blocked)]
    [InlineData(WorkItemStatus.Complete)]
    [InlineData(WorkItemStatus.Failed)]
    public async Task SyncAsync_LocalNonPendingStatus_PushesStatus(WorkItemStatus localStatus)
    {
        // Arrange
        var localItem = CreateWorkItem(externalId: "1", source: "TestSource", status: localStatus);
        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", status: WorkItemStatus.Pending);

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        _workItemRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.Equal(1, result.StatusesPushed);

        _workSourceMock.Verify(
            w => w.UpdateStatusAsync(It.Is<WorkItem>(wi => wi.Status == localStatus), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region SyncAsync - Events

    [Fact]
    public async Task SyncAsync_RaisesSyncStartedEvent()
    {
        // Arrange
        SyncStartedEventArgs? eventArgs = null;
        _syncService.SyncStarted += (_, args) => eventArgs = args;

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _syncService.SyncAsync();

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal("TestSource", eventArgs!.SourceName);
    }

    [Fact]
    public async Task SyncAsync_RaisesSyncCompletedEvent()
    {
        // Arrange
        SyncCompletedEventArgs? eventArgs = null;
        _syncService.SyncCompleted += (_, args) => eventArgs = args;

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _syncService.SyncAsync();

        // Assert
        Assert.NotNull(eventArgs);
        Assert.True(eventArgs!.Result.Success);
    }

    [Fact]
    public async Task SyncAsync_WhenItemAdded_RaisesItemSyncedEvent()
    {
        // Arrange
        var events = new List<ItemSyncedEventArgs>();
        _syncService.ItemSynced += (_, args) => events.Add(args);

        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource");

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _workItemRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        await _syncService.SyncAsync();

        // Assert
        Assert.Single(events, e => e.Action == SyncAction.Added);
    }

    [Fact]
    public async Task SyncAsync_WhenItemUpdated_RaisesItemSyncedEvent()
    {
        // Arrange
        var events = new List<ItemSyncedEventArgs>();
        _syncService.ItemSynced += (_, args) => events.Add(args);

        // Remote has different title to trigger update
        var localItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "Old Title");
        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "New Title");

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        _workItemRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        await _syncService.SyncAsync();

        // Assert
        Assert.Single(events, e => e.Action == SyncAction.Updated);
    }

    [Fact]
    public async Task SyncAsync_WhenItemRemoved_RaisesItemSyncedEvent()
    {
        // Arrange
        var events = new List<ItemSyncedEventArgs>();
        _syncService.ItemSynced += (_, args) => events.Add(args);

        var localItem = CreateWorkItem(externalId: "1", source: "TestSource");

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        // Act
        await _syncService.SyncAsync();

        // Assert
        Assert.Single(events, e => e.Action == SyncAction.Removed);
    }

    [Fact]
    public async Task SyncAsync_WhenStatusPushed_RaisesItemSyncedEvent()
    {
        // Arrange
        var events = new List<ItemSyncedEventArgs>();
        _syncService.ItemSynced += (_, args) => events.Add(args);

        var localItem = CreateWorkItem(externalId: "1", source: "TestSource", status: WorkItemStatus.InProgress);
        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", status: WorkItemStatus.Pending);

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        _workItemRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        await _syncService.SyncAsync();

        // Assert
        Assert.Contains(events, e => e.Action == SyncAction.StatusPushed);
    }

    #endregion

    #region SyncAsync - Concurrent Sync Prevention

    [Fact]
    public async Task SyncAsync_WhenAlreadySyncing_ReturnsFailure()
    {
        // Arrange
        var syncStarted = new TaskCompletionSource();
        var canProceed = new TaskCompletionSource();

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                syncStarted.SetResult();
                await canProceed.Task;
                return [];
            });

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var firstSync = _syncService.SyncAsync();
        await syncStarted.Task; // Wait for first sync to start

        var secondResult = await _syncService.SyncAsync();

        canProceed.SetResult(); // Let first sync complete
        await firstSync;

        // Assert
        Assert.False(secondResult.Success);
        Assert.Contains("already in progress", secondResult.ErrorMessage);
    }

    [Fact]
    public async Task SyncAsync_SetsIsSyncingDuringSync()
    {
        // Arrange
        bool wasSyncing = false;

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                wasSyncing = _syncService.IsSyncing;
                return Task.FromResult<IEnumerable<WorkItem>>([]);
            });

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _syncService.SyncAsync();

        // Assert
        Assert.True(wasSyncing);
        Assert.False(_syncService.IsSyncing);
    }

    #endregion

    #region SyncAsync - Last Sync Time

    [Fact]
    public async Task SyncAsync_OnSuccess_UpdatesLastSyncTime()
    {
        // Arrange
        var before = DateTime.UtcNow;

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _syncService.SyncAsync();
        var after = DateTime.UtcNow;

        // Assert
        Assert.NotNull(_syncService.LastSyncTime);
        Assert.True(_syncService.LastSyncTime >= before);
        Assert.True(_syncService.LastSyncTime <= after);
    }

    [Fact]
    public async Task SyncAsync_OnFailure_DoesNotUpdateLastSyncTime()
    {
        // Arrange
        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test failure"));

        // Act
        await _syncService.SyncAsync();

        // Assert
        Assert.Null(_syncService.LastSyncTime);
    }

    #endregion

    #region SyncAsync - Error Handling

    [Fact]
    public async Task SyncAsync_WhenSourceThrows_ReturnsFailure()
    {
        // Arrange
        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection failed"));

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Connection failed", result.ErrorMessage);
    }

    [Fact]
    public async Task SyncAsync_WhenCancelled_ReturnsFailure()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _syncService.SyncAsync(cts.Token);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("cancelled", result.ErrorMessage);
    }

    [Fact]
    public async Task SyncAsync_OnError_StillRaisesSyncCompletedEvent()
    {
        // Arrange
        SyncCompletedEventArgs? eventArgs = null;
        _syncService.SyncCompleted += (_, args) => eventArgs = args;

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test failure"));

        // Act
        await _syncService.SyncAsync();

        // Assert
        Assert.NotNull(eventArgs);
        Assert.False(eventArgs!.Result.Success);
    }

    #endregion

    #region SyncAsync - Duration Tracking

    [Fact]
    public async Task SyncAsync_TracksDuration()
    {
        // Arrange
        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(50);
                return [];
            });

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.True(result.Duration > TimeSpan.FromMilliseconds(40));
    }

    #endregion

    #region SyncAsync - No-Op Detection (Fix #3)

    [Fact]
    public async Task SyncAsync_WhenContentUnchanged_DoesNotCallUpdate()
    {
        // Arrange: Local and remote have identical content
        var localItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "Same Title");
        localItem.Description = "Same Description";
        localItem.Labels = ["label1", "label2"];

        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "Same Title");
        remoteItem.Description = "Same Description";
        remoteItem.Labels = ["label1", "label2"];

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert: No update should be called since content is identical
        Assert.True(result.Success);
        Assert.Equal(0, result.ItemsUpdated);

        _workItemRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncAsync_WhenLabelsChangedInDifferentOrder_DoesNotCallUpdate()
    {
        // Arrange: Same labels but different order should not trigger update
        var localItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "Title");
        localItem.Labels = ["alpha", "beta", "gamma"];

        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "Title");
        remoteItem.Labels = ["gamma", "alpha", "beta"]; // Same labels, different order

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert: No update since labels are the same (order-insensitive)
        Assert.Equal(0, result.ItemsUpdated);

        _workItemRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncAsync_WhenLabelsActuallyChange_TriggersUpdate()
    {
        // Arrange: Labels are different
        var localItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "Title");
        localItem.Labels = ["alpha", "beta"];

        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "Title");
        remoteItem.Labels = ["alpha", "gamma"]; // Different label

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        _workItemRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.Equal(1, result.ItemsUpdated);

        _workItemRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region SyncAsync - Duplicate ExternalId Handling (Fix #2)

    [Fact]
    public async Task SyncAsync_WithDuplicateLocalExternalIds_UsesNewestItem()
    {
        // Arrange: Two local items with the same ExternalId (shouldn't happen, but handle gracefully)
        var olderItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "Old Duplicate");
        olderItem.UpdatedAt = DateTime.UtcNow.AddDays(-1);

        var newerItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "New Duplicate");
        newerItem.UpdatedAt = DateTime.UtcNow;

        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "Remote Title");

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        // Return both duplicates (older first to ensure we're not just picking the first one)
        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([olderItem, newerItem]);

        _workItemRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert: Should update the newer item (most recently updated)
        Assert.True(result.Success);

        _workItemRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<WorkItem>(w => w.Id == newerItem.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region SyncAsync - Per-Item Error Handling (Fix #7)

    [Fact]
    public async Task SyncAsync_WhenSingleItemFails_ContinuesSyncingOtherItems()
    {
        // Arrange
        var item1 = CreateWorkItem(externalId: "1", source: "TestSource", title: "Item 1");
        var item2 = CreateWorkItem(externalId: "2", source: "TestSource", title: "Item 2");
        var item3 = CreateWorkItem(externalId: "3", source: "TestSource", title: "Item 3");

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([item1, item2, item3]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // First item succeeds, second fails, third succeeds
        var callCount = 0;
        _workItemRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem item, CancellationToken _) =>
            {
                callCount++;
                if (item.ExternalId == "2")
                {
                    throw new Exception("Database error");
                }
                return item;
            });

        // Act
        var result = await _syncService.SyncAsync();

        // Assert: Sync completes with partial success
        Assert.True(result.Success);
        Assert.Equal(2, result.ItemsAdded); // Items 1 and 3 succeeded
        Assert.Contains("1 error", result.ErrorMessage);
        Assert.Contains("Database error", result.ErrorMessage);

        // Verify all three items were attempted
        _workItemRepositoryMock.Verify(
            r => r.CreateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task SyncAsync_WhenStatusPushFails_ContinuesWithOtherItems()
    {
        // Arrange
        var localItem1 = CreateWorkItem(externalId: "1", source: "TestSource", status: WorkItemStatus.InProgress);
        var localItem2 = CreateWorkItem(externalId: "2", source: "TestSource", status: WorkItemStatus.Complete);

        var remoteItem1 = CreateWorkItem(externalId: "1", source: "TestSource", title: "New Title 1", status: WorkItemStatus.Pending);
        var remoteItem2 = CreateWorkItem(externalId: "2", source: "TestSource", title: "New Title 2", status: WorkItemStatus.Pending);

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem1, remoteItem2]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem1, localItem2]);

        _workItemRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // First status push fails, second succeeds
        _workSourceMock.SetupSequence(w => w.UpdateStatusAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("API rate limited"))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.StatusesPushed); // Only second one succeeded
        Assert.Contains("1 error", result.ErrorMessage);
        Assert.Contains("API rate limited", result.ErrorMessage);
    }

    #endregion

    #region SyncAsync - Collection Copying (Fix #4)

    [Fact]
    public async Task SyncAsync_MergedItem_HasIndependentLabelsList()
    {
        // Arrange
        var localItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "Old");
        localItem.Labels = ["original"];

        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "New");
        remoteItem.Labels = ["remote"];

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        WorkItem? capturedItem = null;
        _workItemRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .Callback<WorkItem, CancellationToken>((item, _) => capturedItem = item)
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        await _syncService.SyncAsync();

        // Assert: Modifying the merged item's labels should not affect the remote item
        Assert.NotNull(capturedItem);
        capturedItem!.Labels.Add("modified");

        Assert.DoesNotContain("modified", remoteItem.Labels);
        Assert.Equal(1, remoteItem.Labels.Count);
    }

    [Fact]
    public async Task SyncAsync_MergedItem_HasIndependentDependenciesList()
    {
        // Arrange
        var depId = Guid.NewGuid();
        var localItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "Old");
        localItem.Dependencies = [depId];

        var remoteItem = CreateWorkItem(externalId: "1", source: "TestSource", title: "New");

        _workSourceMock.Setup(w => w.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteItem]);

        _workItemRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([localItem]);

        WorkItem? capturedItem = null;
        _workItemRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .Callback<WorkItem, CancellationToken>((item, _) => capturedItem = item)
            .ReturnsAsync((WorkItem item, CancellationToken _) => item);

        // Act
        await _syncService.SyncAsync();

        // Assert: Modifying the merged item's dependencies should not affect the local item
        Assert.NotNull(capturedItem);
        var newDepId = Guid.NewGuid();
        capturedItem!.Dependencies.Add(newDepId);

        Assert.DoesNotContain(newDepId, localItem.Dependencies);
        Assert.Equal(1, localItem.Dependencies.Count);
    }

    #endregion

    #region Helper Methods

    private static WorkItem CreateWorkItem(
        Guid? id = null,
        string? externalId = null,
        string? source = null,
        string title = "Test Item",
        WorkItemStatus status = WorkItemStatus.Pending)
    {
        return new WorkItem
        {
            Id = id ?? Guid.NewGuid(),
            ExternalId = externalId,
            Source = source,
            Title = title,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion
}

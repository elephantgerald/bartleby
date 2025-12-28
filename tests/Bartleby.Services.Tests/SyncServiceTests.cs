using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Bartleby.Services;
using FluentAssertions;
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
        var act = () => new SyncService(null!, _workItemRepositoryMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("workSource");
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        var act = () => new SyncService(_workSourceMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("workItemRepository");
    }

    #endregion

    #region Initial State Tests

    [Fact]
    public void IsSyncing_Initially_IsFalse()
    {
        _syncService.IsSyncing.Should().BeFalse();
    }

    [Fact]
    public void LastSyncTime_Initially_IsNull()
    {
        _syncService.LastSyncTime.Should().BeNull();
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
        result.Success.Should().BeTrue();
        result.ItemsAdded.Should().Be(1);
        result.ItemsUpdated.Should().Be(0);
        result.ItemsRemoved.Should().Be(0);

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
        result.Success.Should().BeTrue();
        result.ItemsAdded.Should().Be(3);

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
        result.Success.Should().BeTrue();
        result.ItemsAdded.Should().Be(0);

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
        result.Success.Should().BeTrue();
        result.ItemsUpdated.Should().Be(1);
        result.ItemsAdded.Should().Be(0);

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
        capturedItem.Should().NotBeNull();
        capturedItem!.LastWorkedAt.Should().Be(localItem.LastWorkedAt);
        capturedItem.AttemptCount.Should().Be(3);
        capturedItem.BranchName.Should().Be("feature/test");
        capturedItem.ErrorMessage.Should().Be("Previous error");
        capturedItem.Dependencies.Should().HaveCount(1);
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
        result.Success.Should().BeTrue();
        result.ItemsRemoved.Should().Be(1);

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
        result.ItemsRemoved.Should().Be(0);

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
        result.Success.Should().BeTrue();
        result.StatusesPushed.Should().Be(1);

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
        capturedItem.Should().NotBeNull();
        capturedItem!.Status.Should().Be(WorkItemStatus.InProgress);
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
        result.StatusesPushed.Should().Be(0);

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
        result.StatusesPushed.Should().Be(0);

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
        result.StatusesPushed.Should().Be(1);

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
        eventArgs.Should().NotBeNull();
        eventArgs!.SourceName.Should().Be("TestSource");
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
        eventArgs.Should().NotBeNull();
        eventArgs!.Result.Success.Should().BeTrue();
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
        events.Should().ContainSingle(e => e.Action == SyncAction.Added);
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
        events.Should().ContainSingle(e => e.Action == SyncAction.Updated);
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
        events.Should().ContainSingle(e => e.Action == SyncAction.Removed);
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
        events.Should().Contain(e => e.Action == SyncAction.StatusPushed);
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
        secondResult.Success.Should().BeFalse();
        secondResult.ErrorMessage.Should().Contain("already in progress");
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
        wasSyncing.Should().BeTrue();
        _syncService.IsSyncing.Should().BeFalse();
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
        _syncService.LastSyncTime.Should().NotBeNull();
        _syncService.LastSyncTime.Should().BeOnOrAfter(before);
        _syncService.LastSyncTime.Should().BeOnOrBefore(after);
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
        _syncService.LastSyncTime.Should().BeNull();
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
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Connection failed");
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
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
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
        eventArgs.Should().NotBeNull();
        eventArgs!.Result.Success.Should().BeFalse();
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
        result.Duration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(40));
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
        result.Success.Should().BeTrue();
        result.ItemsUpdated.Should().Be(0);

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
        result.ItemsUpdated.Should().Be(0);

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
        result.ItemsUpdated.Should().Be(1);

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
        result.Success.Should().BeTrue();

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
        result.Success.Should().BeTrue();
        result.ItemsAdded.Should().Be(2); // Items 1 and 3 succeeded
        result.ErrorMessage.Should().Contain("1 error");
        result.ErrorMessage.Should().Contain("Database error");

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
        result.Success.Should().BeTrue();
        result.StatusesPushed.Should().Be(1); // Only second one succeeded
        result.ErrorMessage.Should().Contain("1 error");
        result.ErrorMessage.Should().Contain("API rate limited");
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
        capturedItem.Should().NotBeNull();
        capturedItem!.Labels.Add("modified");

        remoteItem.Labels.Should().NotContain("modified");
        remoteItem.Labels.Should().HaveCount(1);
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
        capturedItem.Should().NotBeNull();
        var newDepId = Guid.NewGuid();
        capturedItem!.Dependencies.Add(newDepId);

        localItem.Dependencies.Should().NotContain(newDepId);
        localItem.Dependencies.Should().HaveCount(1);
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

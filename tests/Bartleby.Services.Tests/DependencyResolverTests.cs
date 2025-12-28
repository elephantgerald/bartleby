using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Bartleby.Services;
using FluentAssertions;
using Moq;

namespace Bartleby.Services.Tests;

public class DependencyResolverTests
{
    private readonly Mock<IGraphStore> _graphStoreMock;
    private readonly Mock<IWorkItemRepository> _workItemRepositoryMock;
    private readonly DependencyResolver _resolver;

    public DependencyResolverTests()
    {
        _graphStoreMock = new Mock<IGraphStore>();
        _workItemRepositoryMock = new Mock<IWorkItemRepository>();
        _resolver = new DependencyResolver(_graphStoreMock.Object, _workItemRepositoryMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullGraphStore_ThrowsArgumentNullException()
    {
        var act = () => new DependencyResolver(null!, _workItemRepositoryMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("graphStore");
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        var act = () => new DependencyResolver(_graphStoreMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("workItemRepository");
    }

    #endregion

    #region GetReadyWorkItemsAsync - No Dependencies

    [Fact]
    public async Task GetReadyWorkItemsAsync_WithNoDependencies_ReturnsAllPendingItems()
    {
        // Arrange
        var workItems = new List<WorkItem>
        {
            CreateWorkItem(status: WorkItemStatus.Pending),
            CreateWorkItem(status: WorkItemStatus.Pending),
            CreateWorkItem(status: WorkItemStatus.Ready)
        };

        SetupMocks(new DependencyGraph(), workItems);

        // Act
        var result = await _resolver.GetReadyWorkItemsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetReadyWorkItemsAsync_WithNoWorkItems_ReturnsEmpty()
    {
        // Arrange
        SetupMocks(new DependencyGraph(), []);

        // Act
        var result = await _resolver.GetReadyWorkItemsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReadyWorkItemsAsync_ExcludesCompletedItems()
    {
        // Arrange
        var pendingItem = CreateWorkItem(status: WorkItemStatus.Pending);
        var completedItem = CreateWorkItem(status: WorkItemStatus.Complete);
        var workItems = new List<WorkItem> { pendingItem, completedItem };

        SetupMocks(new DependencyGraph(), workItems);

        // Act
        var result = await _resolver.GetReadyWorkItemsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(pendingItem);
        result.Should().NotContain(completedItem);
    }

    [Fact]
    public async Task GetReadyWorkItemsAsync_ExcludesInProgressItems()
    {
        // Arrange
        var pendingItem = CreateWorkItem(status: WorkItemStatus.Pending);
        var inProgressItem = CreateWorkItem(status: WorkItemStatus.InProgress);
        var workItems = new List<WorkItem> { pendingItem, inProgressItem };

        SetupMocks(new DependencyGraph(), workItems);

        // Act
        var result = await _resolver.GetReadyWorkItemsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(pendingItem);
    }

    [Fact]
    public async Task GetReadyWorkItemsAsync_ExcludesBlockedItems()
    {
        // Arrange
        var pendingItem = CreateWorkItem(status: WorkItemStatus.Pending);
        var blockedItem = CreateWorkItem(status: WorkItemStatus.Blocked);
        var workItems = new List<WorkItem> { pendingItem, blockedItem };

        SetupMocks(new DependencyGraph(), workItems);

        // Act
        var result = await _resolver.GetReadyWorkItemsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(pendingItem);
    }

    [Fact]
    public async Task GetReadyWorkItemsAsync_ExcludesFailedItems()
    {
        // Arrange
        var pendingItem = CreateWorkItem(status: WorkItemStatus.Pending);
        var failedItem = CreateWorkItem(status: WorkItemStatus.Failed);
        var workItems = new List<WorkItem> { pendingItem, failedItem };

        SetupMocks(new DependencyGraph(), workItems);

        // Act
        var result = await _resolver.GetReadyWorkItemsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(pendingItem);
    }

    #endregion

    #region GetReadyWorkItemsAsync - Met Dependencies

    [Fact]
    public async Task GetReadyWorkItemsAsync_WithMetDependencies_ReturnsReadyItem()
    {
        // Arrange: B depends on A, A is complete
        var itemA = CreateWorkItem(status: WorkItemStatus.Complete);
        var itemB = CreateWorkItem(status: WorkItemStatus.Pending);

        var graph = CreateGraph([(itemB.Id, itemA.Id)], [itemA.Id, itemB.Id]);
        var workItems = new List<WorkItem> { itemA, itemB };

        SetupMocks(graph, workItems);

        // Act
        var result = await _resolver.GetReadyWorkItemsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(itemB);
    }

    [Fact]
    public async Task GetReadyWorkItemsAsync_WithMultipleMetDependencies_ReturnsReadyItem()
    {
        // Arrange: C depends on A and B, both are complete
        var itemA = CreateWorkItem(status: WorkItemStatus.Complete);
        var itemB = CreateWorkItem(status: WorkItemStatus.Complete);
        var itemC = CreateWorkItem(status: WorkItemStatus.Pending);

        var graph = CreateGraph(
            [(itemC.Id, itemA.Id), (itemC.Id, itemB.Id)],
            [itemA.Id, itemB.Id, itemC.Id]);
        var workItems = new List<WorkItem> { itemA, itemB, itemC };

        SetupMocks(graph, workItems);

        // Act
        var result = await _resolver.GetReadyWorkItemsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(itemC);
    }

    [Fact]
    public async Task GetReadyWorkItemsAsync_WithChainOfDependencies_ReturnsFirstInChain()
    {
        // Arrange: C -> B -> A (C depends on B, B depends on A)
        // A is pending, B is pending, C is pending
        // Only A should be ready
        var itemA = CreateWorkItem(status: WorkItemStatus.Pending);
        var itemB = CreateWorkItem(status: WorkItemStatus.Pending);
        var itemC = CreateWorkItem(status: WorkItemStatus.Pending);

        var graph = CreateGraph(
            [(itemB.Id, itemA.Id), (itemC.Id, itemB.Id)],
            [itemA.Id, itemB.Id, itemC.Id]);
        var workItems = new List<WorkItem> { itemA, itemB, itemC };

        SetupMocks(graph, workItems);

        // Act
        var result = await _resolver.GetReadyWorkItemsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(itemA);
    }

    #endregion

    #region GetReadyWorkItemsAsync - Unmet Dependencies

    [Fact]
    public async Task GetReadyWorkItemsAsync_WithUnmetDependencies_DoesNotReturnBlockedItem()
    {
        // Arrange: B depends on A, A is still pending
        var itemA = CreateWorkItem(status: WorkItemStatus.Pending);
        var itemB = CreateWorkItem(status: WorkItemStatus.Pending);

        var graph = CreateGraph([(itemB.Id, itemA.Id)], [itemA.Id, itemB.Id]);
        var workItems = new List<WorkItem> { itemA, itemB };

        SetupMocks(graph, workItems);

        // Act
        var result = await _resolver.GetReadyWorkItemsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(itemA);
        result.Should().NotContain(itemB);
    }

    [Fact]
    public async Task GetReadyWorkItemsAsync_WithPartiallyMetDependencies_DoesNotReturnItem()
    {
        // Arrange: C depends on A (complete) and B (pending)
        var itemA = CreateWorkItem(status: WorkItemStatus.Complete);
        var itemB = CreateWorkItem(status: WorkItemStatus.Pending);
        var itemC = CreateWorkItem(status: WorkItemStatus.Pending);

        var graph = CreateGraph(
            [(itemC.Id, itemA.Id), (itemC.Id, itemB.Id)],
            [itemA.Id, itemB.Id, itemC.Id]);
        var workItems = new List<WorkItem> { itemA, itemB, itemC };

        SetupMocks(graph, workItems);

        // Act
        var result = await _resolver.GetReadyWorkItemsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(itemB);
        result.Should().NotContain(itemC);
    }

    #endregion

    #region GetReadyWorkItemsAsync - Ordering

    [Fact]
    public async Task GetReadyWorkItemsAsync_OrdersByCreatedAtAscending()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var oldItem = CreateWorkItem(status: WorkItemStatus.Pending);
        oldItem.CreatedAt = now.AddDays(-2);

        var newItem = CreateWorkItem(status: WorkItemStatus.Pending);
        newItem.CreatedAt = now;

        var middleItem = CreateWorkItem(status: WorkItemStatus.Pending);
        middleItem.CreatedAt = now.AddDays(-1);

        var workItems = new List<WorkItem> { newItem, oldItem, middleItem };

        SetupMocks(new DependencyGraph(), workItems);

        // Act
        var result = await _resolver.GetReadyWorkItemsAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().Be(oldItem);
        result[1].Should().Be(middleItem);
        result[2].Should().Be(newItem);
    }

    #endregion

    #region IsReadyAsync

    [Fact]
    public async Task IsReadyAsync_ItemNotInGraph_ReturnsTrue()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        SetupMocks(new DependencyGraph(), []);

        // Act
        var result = await _resolver.IsReadyAsync(itemId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsReadyAsync_ItemWithNoDependencies_ReturnsTrue()
    {
        // Arrange
        var item = CreateWorkItem(status: WorkItemStatus.Pending);
        var graph = CreateGraph([], [item.Id]);
        SetupMocks(graph, [item]);

        // Act
        var result = await _resolver.IsReadyAsync(item.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsReadyAsync_ItemWithCompleteDependencies_ReturnsTrue()
    {
        // Arrange
        var itemA = CreateWorkItem(status: WorkItemStatus.Complete);
        var itemB = CreateWorkItem(status: WorkItemStatus.Pending);

        var graph = CreateGraph([(itemB.Id, itemA.Id)], [itemA.Id, itemB.Id]);
        SetupMocks(graph, [itemA, itemB]);

        // Act
        var result = await _resolver.IsReadyAsync(itemB.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsReadyAsync_ItemWithIncompleteDependencies_ReturnsFalse()
    {
        // Arrange
        var itemA = CreateWorkItem(status: WorkItemStatus.Pending);
        var itemB = CreateWorkItem(status: WorkItemStatus.Pending);

        var graph = CreateGraph([(itemB.Id, itemA.Id)], [itemA.Id, itemB.Id]);
        SetupMocks(graph, [itemA, itemB]);

        // Act
        var result = await _resolver.IsReadyAsync(itemB.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsReadyAsync_ItemInCycle_ReturnsFalse()
    {
        // Arrange: A and B form a cycle (A -> B -> A)
        var itemA = CreateWorkItem(status: WorkItemStatus.Pending);
        var itemB = CreateWorkItem(status: WorkItemStatus.Pending);

        var graph = CreateGraph(
            [(itemA.Id, itemB.Id), (itemB.Id, itemA.Id)],
            [itemA.Id, itemB.Id]);
        SetupMocks(graph, [itemA, itemB]);

        // Act
        var resultA = await _resolver.IsReadyAsync(itemA.Id);
        var resultB = await _resolver.IsReadyAsync(itemB.Id);

        // Assert - both items in the cycle should return false
        resultA.Should().BeFalse();
        resultB.Should().BeFalse();
    }

    #endregion

    #region DetectCircularDependenciesAsync

    [Fact]
    public async Task DetectCircularDependenciesAsync_NoCycles_ReturnsEmpty()
    {
        // Arrange: A -> B -> C (linear chain, no cycle)
        var itemA = CreateWorkItem();
        var itemB = CreateWorkItem();
        var itemC = CreateWorkItem();

        var graph = CreateGraph(
            [(itemB.Id, itemA.Id), (itemC.Id, itemB.Id)],
            [itemA.Id, itemB.Id, itemC.Id]);
        SetupMocks(graph, [itemA, itemB, itemC]);

        // Act
        var result = await _resolver.DetectCircularDependenciesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectCircularDependenciesAsync_SimpleCycle_ReturnsCycle()
    {
        // Arrange: A -> B -> A (direct cycle)
        var itemA = CreateWorkItem();
        var itemB = CreateWorkItem();

        var graph = CreateGraph(
            [(itemA.Id, itemB.Id), (itemB.Id, itemA.Id)],
            [itemA.Id, itemB.Id]);
        SetupMocks(graph, [itemA, itemB]);

        // Act
        var result = await _resolver.DetectCircularDependenciesAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.SelectMany(c => c).Should().Contain(itemA.Id);
        result.SelectMany(c => c).Should().Contain(itemB.Id);
    }

    [Fact]
    public async Task DetectCircularDependenciesAsync_LongerCycle_ReturnsCycle()
    {
        // Arrange: A -> B -> C -> A (3-node cycle)
        var itemA = CreateWorkItem();
        var itemB = CreateWorkItem();
        var itemC = CreateWorkItem();

        var graph = CreateGraph(
            [(itemA.Id, itemB.Id), (itemB.Id, itemC.Id), (itemC.Id, itemA.Id)],
            [itemA.Id, itemB.Id, itemC.Id]);
        SetupMocks(graph, [itemA, itemB, itemC]);

        // Act
        var result = await _resolver.DetectCircularDependenciesAsync();

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DetectCircularDependenciesAsync_SelfLoop_ReturnsCycle()
    {
        // Arrange: A -> A (self-referential)
        var itemA = CreateWorkItem();

        var graph = CreateGraph([(itemA.Id, itemA.Id)], [itemA.Id]);
        SetupMocks(graph, [itemA]);

        // Act
        var result = await _resolver.DetectCircularDependenciesAsync();

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DetectCircularDependenciesAsync_EmptyGraph_ReturnsEmpty()
    {
        // Arrange
        SetupMocks(new DependencyGraph(), []);

        // Act
        var result = await _resolver.DetectCircularDependenciesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetDependencyChainAsync

    [Fact]
    public async Task GetDependencyChainAsync_ItemNotInGraph_ReturnsEmpty()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        SetupMocks(new DependencyGraph(), []);

        // Act
        var result = await _resolver.GetDependencyChainAsync(itemId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDependencyChainAsync_ItemWithNoDependencies_ReturnsEmpty()
    {
        // Arrange
        var item = CreateWorkItem();
        var graph = CreateGraph([], [item.Id]);
        SetupMocks(graph, [item]);

        // Act
        var result = await _resolver.GetDependencyChainAsync(item.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDependencyChainAsync_ItemWithOneDependency_ReturnsDependency()
    {
        // Arrange
        var itemA = CreateWorkItem();
        var itemB = CreateWorkItem();

        var graph = CreateGraph([(itemB.Id, itemA.Id)], [itemA.Id, itemB.Id]);
        SetupMocks(graph, [itemA, itemB]);

        // Act
        var result = await _resolver.GetDependencyChainAsync(itemB.Id);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(itemA.Id);
    }

    [Fact]
    public async Task GetDependencyChainAsync_ItemWithChainedDependencies_ReturnsAllDependencies()
    {
        // Arrange: C -> B -> A
        var itemA = CreateWorkItem();
        var itemB = CreateWorkItem();
        var itemC = CreateWorkItem();

        var graph = CreateGraph(
            [(itemB.Id, itemA.Id), (itemC.Id, itemB.Id)],
            [itemA.Id, itemB.Id, itemC.Id]);
        SetupMocks(graph, [itemA, itemB, itemC]);

        // Act
        var result = await _resolver.GetDependencyChainAsync(itemC.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(itemA.Id);
        result.Should().Contain(itemB.Id);
    }

    #endregion

    #region ResolveAsync

    [Fact]
    public async Task ResolveAsync_WithMixedScenario_ReturnsCorrectCategorization()
    {
        // Arrange:
        // - A is standalone (ready)
        // - B depends on A (blocked because A is pending)
        // - C and D form a cycle
        var itemA = CreateWorkItem(status: WorkItemStatus.Pending);
        var itemB = CreateWorkItem(status: WorkItemStatus.Pending);
        var itemC = CreateWorkItem(status: WorkItemStatus.Pending);
        var itemD = CreateWorkItem(status: WorkItemStatus.Pending);

        var graph = CreateGraph(
            [
                (itemB.Id, itemA.Id),    // B depends on A
                (itemC.Id, itemD.Id),    // C depends on D
                (itemD.Id, itemC.Id)     // D depends on C (cycle!)
            ],
            [itemA.Id, itemB.Id, itemC.Id, itemD.Id]);

        SetupMocks(graph, [itemA, itemB, itemC, itemD]);

        // Act
        var result = await _resolver.ResolveAsync();

        // Assert
        result.ReadyItems.Should().Contain(itemA);
        result.BlockedItems.Should().Contain(itemB);
        result.HasCycles.Should().BeTrue();
        result.CyclicItems.Should().Contain(itemC);
        result.CyclicItems.Should().Contain(itemD);
    }

    [Fact]
    public async Task ResolveAsync_WithNoCycles_HasCyclesIsFalse()
    {
        // Arrange
        var item = CreateWorkItem(status: WorkItemStatus.Pending);
        SetupMocks(new DependencyGraph(), [item]);

        // Act
        var result = await _resolver.ResolveAsync();

        // Assert
        result.HasCycles.Should().BeFalse();
        result.Cycles.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static WorkItem CreateWorkItem(WorkItemStatus status = WorkItemStatus.Pending)
    {
        return new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = $"Test Work Item {Guid.NewGuid():N}",
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static DependencyGraph CreateGraph(
        List<(Guid dependent, Guid dependsOn)> edges,
        List<Guid> nodeIds)
    {
        var graph = new DependencyGraph();

        // Add all nodes
        foreach (var nodeId in nodeIds)
        {
            graph.Nodes[nodeId] = new DependencyNode
            {
                WorkItemId = nodeId,
                Title = $"Node {nodeId}"
            };
        }

        // Add edges
        foreach (var (dependent, dependsOn) in edges)
        {
            graph.AddDependency(dependent, dependsOn);
        }

        return graph;
    }

    private void SetupMocks(DependencyGraph graph, List<WorkItem> workItems)
    {
        _graphStoreMock
            .Setup(g => g.LoadGraphAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);

        _workItemRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
    }

    #endregion
}

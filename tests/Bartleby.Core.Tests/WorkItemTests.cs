using Bartleby.Core.Models;
using FluentAssertions;

namespace Bartleby.Core.Tests;

public class WorkItemTests
{
    [Fact]
    public void NewWorkItem_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var workItem = new WorkItem();

        // Assert
        workItem.Id.Should().NotBeEmpty();
        workItem.Title.Should().BeEmpty();
        workItem.Description.Should().BeEmpty();
        workItem.Status.Should().Be(WorkItemStatus.Pending);
        workItem.Dependencies.Should().BeEmpty();
        workItem.Labels.Should().BeEmpty();
        workItem.AttemptCount.Should().Be(0);
    }

    [Fact]
    public void WorkItem_ShouldInitializeWithProvidedValues()
    {
        // Arrange
        var title = "Test Work Item";
        var description = "A test description";

        // Act
        var workItem = new WorkItem
        {
            Title = title,
            Description = description,
            Status = WorkItemStatus.Ready
        };

        // Assert
        workItem.Title.Should().Be(title);
        workItem.Description.Should().Be(description);
        workItem.Status.Should().Be(WorkItemStatus.Ready);
    }

    [Theory]
    [InlineData(WorkItemStatus.Pending)]
    [InlineData(WorkItemStatus.Ready)]
    [InlineData(WorkItemStatus.InProgress)]
    [InlineData(WorkItemStatus.Blocked)]
    [InlineData(WorkItemStatus.Complete)]
    [InlineData(WorkItemStatus.Failed)]
    public void WorkItem_ShouldAcceptAllStatusValues(WorkItemStatus status)
    {
        // Arrange & Act
        var workItem = new WorkItem { Status = status };

        // Assert
        workItem.Status.Should().Be(status);
    }
}

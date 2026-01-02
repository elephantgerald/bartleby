using Bartleby.Core.Models;

namespace Bartleby.Core.Tests;

public class WorkItemTests
{
    [Fact]
    public void NewWorkItem_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var workItem = new WorkItem();

        // Assert
        Assert.NotEqual(Guid.Empty, workItem.Id);
        Assert.Empty(workItem.Title);
        Assert.Empty(workItem.Description);
        Assert.Equal(WorkItemStatus.Pending, workItem.Status);
        Assert.Empty(workItem.Dependencies);
        Assert.Empty(workItem.Labels);
        Assert.Equal(0, workItem.AttemptCount);
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
        Assert.Equal(title, workItem.Title);
        Assert.Equal(description, workItem.Description);
        Assert.Equal(WorkItemStatus.Ready, workItem.Status);
    }

    [Fact]
    public void WorkItem_ShouldAcceptAllStatusValues()
    {
        foreach (var status in Enum.GetValues<WorkItemStatus>())
        {
            // Arrange & Act
            var workItem = new WorkItem { Status = status };

            // Assert
            Assert.Equal(status, workItem.Status);
        }
    }
}

using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;

namespace Bartleby.Infrastructure.WorkSources;

/// <summary>
/// Stub work source that returns mock data for testing.
/// </summary>
public class StubWorkSource : IWorkSource
{
    public string Name => "Stub";

    public Task<IEnumerable<WorkItem>> SyncAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<WorkItem>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Title = "Implement user authentication",
                Description = "Add login and logout functionality with JWT tokens.",
                Status = WorkItemStatus.Ready,
                ExternalId = "1",
                Source = Name,
                Labels = ["feature", "auth"]
            },
            new()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Title = "Add unit tests for auth module",
                Description = "Write comprehensive unit tests for the authentication module.",
                Status = WorkItemStatus.Pending,
                ExternalId = "2",
                Source = Name,
                Labels = ["test"],
                Dependencies = [Guid.Parse("11111111-1111-1111-1111-111111111111")]
            },
            new()
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Title = "Fix navigation bug",
                Description = "Users report that back button doesn't work on settings page.",
                Status = WorkItemStatus.Ready,
                ExternalId = "3",
                Source = Name,
                Labels = ["bug", "navigation"]
            }
        };

        return Task.FromResult<IEnumerable<WorkItem>>(items);
    }

    public Task UpdateStatusAsync(WorkItem workItem, CancellationToken cancellationToken = default)
    {
        // Stub - no-op
        return Task.CompletedTask;
    }

    public Task AddCommentAsync(WorkItem workItem, string comment, CancellationToken cancellationToken = default)
    {
        // Stub - no-op
        return Task.CompletedTask;
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}

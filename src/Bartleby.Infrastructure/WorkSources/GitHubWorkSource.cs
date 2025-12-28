using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;

namespace Bartleby.Infrastructure.WorkSources;

/// <summary>
/// Work source that syncs work items from GitHub Issues.
/// </summary>
public class GitHubWorkSource : IWorkSource
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly Func<string?, IGitHubApiClient> _clientFactory;

    private IGitHubApiClient? _client;
    private string? _lastToken;

    public string Name => "GitHub";

    /// <summary>
    /// Creates a new GitHubWorkSource with the default client factory.
    /// </summary>
    public GitHubWorkSource(ISettingsRepository settingsRepository)
        : this(settingsRepository, token => new OctokitGitHubApiClient(token))
    {
    }

    /// <summary>
    /// Creates a new GitHubWorkSource with a custom client factory (for testing).
    /// </summary>
    public GitHubWorkSource(
        ISettingsRepository settingsRepository,
        Func<string?, IGitHubApiClient> clientFactory)
    {
        _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    public async Task<IEnumerable<WorkItem>> SyncAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);

        if (string.IsNullOrEmpty(settings.GitHubOwner) || string.IsNullOrEmpty(settings.GitHubRepo))
        {
            return [];
        }

        var client = GetClient(settings.GitHubToken);
        var issues = await client.GetIssuesAsync(settings.GitHubOwner, settings.GitHubRepo, cancellationToken);

        return issues
            .Where(i => !i.IsPullRequest)
            .Select(i => MapIssueToWorkItem(i))
            .ToList();
    }

    public async Task UpdateStatusAsync(WorkItem workItem, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(workItem.ExternalId))
        {
            throw new ArgumentException("WorkItem must have an ExternalId to update status.", nameof(workItem));
        }

        var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);

        if (string.IsNullOrEmpty(settings.GitHubOwner) || string.IsNullOrEmpty(settings.GitHubRepo))
        {
            throw new InvalidOperationException("GitHub owner and repo must be configured.");
        }

        if (!int.TryParse(workItem.ExternalId, out var issueNumber))
        {
            throw new ArgumentException($"ExternalId '{workItem.ExternalId}' is not a valid issue number.", nameof(workItem));
        }

        var client = GetClient(settings.GitHubToken);

        // Build labels list with status label
        var labels = new List<string>(workItem.Labels);
        var statusLabel = GetStatusLabel(workItem.Status);
        if (!string.IsNullOrEmpty(statusLabel))
        {
            labels.Add(statusLabel);
        }

        var update = new GitHubIssueUpdate
        {
            IsClosed = workItem.Status == WorkItemStatus.Complete,
            Labels = labels
        };

        await client.UpdateIssueAsync(settings.GitHubOwner, settings.GitHubRepo, issueNumber, update, cancellationToken);
    }

    public async Task AddCommentAsync(WorkItem workItem, string comment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(workItem.ExternalId))
        {
            throw new ArgumentException("WorkItem must have an ExternalId to add a comment.", nameof(workItem));
        }

        if (string.IsNullOrEmpty(comment))
        {
            throw new ArgumentException("Comment cannot be empty.", nameof(comment));
        }

        var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);

        if (string.IsNullOrEmpty(settings.GitHubOwner) || string.IsNullOrEmpty(settings.GitHubRepo))
        {
            throw new InvalidOperationException("GitHub owner and repo must be configured.");
        }

        if (!int.TryParse(workItem.ExternalId, out var issueNumber))
        {
            throw new ArgumentException($"ExternalId '{workItem.ExternalId}' is not a valid issue number.", nameof(workItem));
        }

        var client = GetClient(settings.GitHubToken);
        await client.AddCommentAsync(settings.GitHubOwner, settings.GitHubRepo, issueNumber, comment, cancellationToken);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);

            if (string.IsNullOrEmpty(settings.GitHubOwner) || string.IsNullOrEmpty(settings.GitHubRepo))
            {
                return false;
            }

            var client = GetClient(settings.GitHubToken);
            return await client.TestConnectionAsync(settings.GitHubOwner, settings.GitHubRepo, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private IGitHubApiClient GetClient(string? token)
    {
        // Recreate client if token changed
        if (_client == null || _lastToken != token)
        {
            _client = _clientFactory(token);
            _lastToken = token;
        }
        return _client;
    }

    private WorkItem MapIssueToWorkItem(GitHubIssue issue)
    {
        return new WorkItem
        {
            Id = GenerateConsistentGuid(Name, issue.Number.ToString()),
            Title = issue.Title ?? string.Empty,
            Description = issue.Body ?? string.Empty,
            Status = MapLabelsToStatus(issue.Labels),
            ExternalId = issue.Number.ToString(),
            Source = Name,
            ExternalUrl = issue.HtmlUrl,
            Labels = issue.Labels.ToList(),
            CreatedAt = issue.CreatedAt.UtcDateTime,
            UpdatedAt = issue.UpdatedAt?.UtcDateTime ?? issue.CreatedAt.UtcDateTime
        };
    }

    private static WorkItemStatus MapLabelsToStatus(IReadOnlyList<string> labels)
    {
        if (labels.Count == 0)
        {
            return WorkItemStatus.Pending;
        }

        var labelNames = labels.Select(l => l.ToLowerInvariant()).ToHashSet();

        // Check for status labels in priority order
        if (labelNames.Contains("bartleby:in-progress") || labelNames.Contains("in progress"))
        {
            return WorkItemStatus.InProgress;
        }

        if (labelNames.Contains("bartleby:blocked") || labelNames.Contains("blocked"))
        {
            return WorkItemStatus.Blocked;
        }

        if (labelNames.Contains("bartleby:failed") || labelNames.Contains("failed"))
        {
            return WorkItemStatus.Failed;
        }

        if (labelNames.Contains("bartleby:ready") || labelNames.Contains("ready"))
        {
            return WorkItemStatus.Ready;
        }

        return WorkItemStatus.Pending;
    }

    private static string? GetStatusLabel(WorkItemStatus status)
    {
        return status switch
        {
            WorkItemStatus.Ready => "bartleby:ready",
            WorkItemStatus.InProgress => "bartleby:in-progress",
            WorkItemStatus.Blocked => "bartleby:blocked",
            WorkItemStatus.Failed => "bartleby:failed",
            WorkItemStatus.Complete => null, // No label needed, issue is closed
            _ => null
        };
    }

    /// <summary>
    /// Generates a consistent GUID for a work item based on its source and external ID.
    /// This ensures the same issue always maps to the same WorkItem ID.
    /// </summary>
    private static Guid GenerateConsistentGuid(string source, string externalId)
    {
        var input = $"{source}:{externalId}";
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Octokit;

namespace Bartleby.Infrastructure.WorkSources;

/// <summary>
/// Octokit-based implementation of IGitHubApiClient.
/// </summary>
public class OctokitGitHubApiClient : IGitHubApiClient
{
    private readonly GitHubClient _client;
    private readonly ILogger<OctokitGitHubApiClient> _logger;

    public OctokitGitHubApiClient(string? token, ILogger<OctokitGitHubApiClient>? logger = null)
    {
        _logger = logger ?? NullLogger<OctokitGitHubApiClient>.Instance;
        _client = new GitHubClient(new ProductHeaderValue("Bartleby"));

        if (!string.IsNullOrEmpty(token))
        {
            _client.Credentials = new Credentials(token);
            _logger.LogDebug("GitHub client initialized with authentication token");
        }
        else
        {
            _logger.LogDebug("GitHub client initialized without authentication (anonymous access)");
        }
    }

    public async Task<IReadOnlyList<GitHubIssue>> GetIssuesAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default)
    {
        var request = new RepositoryIssueRequest
        {
            State = ItemStateFilter.Open
        };

        var options = new ApiOptions
        {
            PageSize = 100,
            PageCount = 1
        };

        var allIssues = new List<Issue>();
        var page = 1;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            options.StartPage = page;
            var issues = await _client.Issue.GetAllForRepository(owner, repo, request, options);

            if (issues.Count == 0)
            {
                break;
            }

            allIssues.AddRange(issues);

            if (issues.Count < options.PageSize)
            {
                break;
            }

            page++;
        }

        return allIssues
            .Select(i => new GitHubIssue(
                Number: i.Number,
                Title: i.Title ?? string.Empty,
                Body: i.Body,
                HtmlUrl: i.HtmlUrl?.ToString(),
                Labels: i.Labels?.Select(l => l.Name).ToList() ?? [],
                CreatedAt: i.CreatedAt,
                UpdatedAt: i.UpdatedAt,
                IsPullRequest: i.PullRequest != null))
            .ToList();
    }

    public async Task UpdateIssueAsync(
        string owner,
        string repo,
        int issueNumber,
        GitHubIssueUpdate update,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var issueUpdate = new IssueUpdate();

        if (update.IsClosed.HasValue)
        {
            issueUpdate.State = update.IsClosed.Value ? ItemState.Closed : ItemState.Open;
        }

        if (update.Labels != null)
        {
            issueUpdate.ClearLabels();
            foreach (var label in update.Labels)
            {
                issueUpdate.AddLabel(label);
            }
        }

        await _client.Issue.Update(owner, repo, issueNumber, issueUpdate);
    }

    public async Task AddCommentAsync(
        string owner,
        string repo,
        int issueNumber,
        string comment,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _client.Issue.Comment.Create(owner, repo, issueNumber, comment);
    }

    public async Task<bool> TestConnectionAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Testing connection to repository {Owner}/{Repo}", owner, repo);
            var repository = await _client.Repository.Get(owner, repo);
            return repository != null;
        }
        catch (AuthorizationException ex)
        {
            _logger.LogWarning(ex, "GitHub API authorization failed for {Owner}/{Repo}. Check your token permissions.", owner, repo);
            return false;
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "GitHub repository {Owner}/{Repo} not found. Check the owner and repo names.", owner, repo);
            return false;
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "GitHub API error testing connection to {Owner}/{Repo}: {Message}", owner, repo, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error testing GitHub connection to {Owner}/{Repo}", owner, repo);
            return false;
        }
    }
}

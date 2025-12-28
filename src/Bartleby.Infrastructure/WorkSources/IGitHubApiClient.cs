namespace Bartleby.Infrastructure.WorkSources;

/// <summary>
/// Abstraction over the Octokit GitHub API for testability.
/// </summary>
public interface IGitHubApiClient
{
    /// <summary>
    /// Gets all open issues for a repository.
    /// </summary>
    Task<IReadOnlyList<GitHubIssue>> GetIssuesAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an issue.
    /// </summary>
    Task UpdateIssueAsync(
        string owner,
        string repo,
        int issueNumber,
        GitHubIssueUpdate update,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a comment to an issue.
    /// </summary>
    Task AddCommentAsync(
        string owner,
        string repo,
        int issueNumber,
        string comment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connection by getting repository info.
    /// </summary>
    Task<bool> TestConnectionAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Simple DTO representing a GitHub issue.
/// </summary>
public record GitHubIssue(
    int Number,
    string Title,
    string? Body,
    string? HtmlUrl,
    IReadOnlyList<string> Labels,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsPullRequest);

/// <summary>
/// DTO for updating a GitHub issue.
/// </summary>
public class GitHubIssueUpdate
{
    public bool? IsClosed { get; set; }
    public IReadOnlyList<string>? Labels { get; set; }
}

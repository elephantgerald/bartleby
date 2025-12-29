using System.Text;
using System.Text.RegularExpressions;
using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace Bartleby.Infrastructure.Git;

/// <summary>
/// Git service implementation using LibGit2Sharp.
/// </summary>
public partial class GitService : IGitService
{
    private readonly ILogger<GitService> _logger;
    private readonly Func<string, IRepositoryWrapper>? _repositoryFactory;

    /// <summary>
    /// Creates a new GitService for production use.
    /// </summary>
    public GitService(ILogger<GitService> logger)
        : this(logger, null)
    {
    }

    /// <summary>
    /// Creates a new GitService with an optional factory for testing.
    /// </summary>
    internal GitService(
        ILogger<GitService> logger,
        Func<string, IRepositoryWrapper>? repositoryFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repositoryFactory = repositoryFactory;
    }

    public bool IsGitRepository(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        try
        {
            var repoPath = Repository.Discover(workingDirectory);
            return !string.IsNullOrEmpty(repoPath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Directory {Directory} is not a git repository", workingDirectory);
            return false;
        }
    }

    public Task<GitOperationResult> InitializeRepositoryAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        try
        {
            if (IsGitRepository(workingDirectory))
            {
                _logger.LogInformation("Repository already exists at {Directory}", workingDirectory);
                return Task.FromResult(GitOperationResult.Succeeded("Repository already exists"));
            }

            Repository.Init(workingDirectory);
            _logger.LogInformation("Initialized new git repository at {Directory}", workingDirectory);
            return Task.FromResult(GitOperationResult.Succeeded("Repository initialized"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize repository at {Directory}", workingDirectory);
            return Task.FromResult(GitOperationResult.Failed($"Failed to initialize repository: {ex.Message}"));
        }
    }

    public Task<GitOperationResult> CreateOrSwitchToBranchAsync(
        WorkItem workItem,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        try
        {
            var branchName = GenerateBranchName(workItem);

            using var repo = OpenRepository(workingDirectory);

            // Check if branch already exists
            var existingBranch = repo.Branches[branchName];

            if (existingBranch != null)
            {
                // Branch exists, switch to it
                if (repo.Head.FriendlyName != branchName)
                {
                    repo.Checkout(existingBranch);
                    _logger.LogInformation("Switched to existing branch {BranchName}", branchName);
                }
                else
                {
                    _logger.LogDebug("Already on branch {BranchName}", branchName);
                }

                return Task.FromResult(GitOperationResult.SucceededWithBranch(branchName, "Switched to existing branch"));
            }

            // Create new branch from current HEAD
            var newBranch = repo.CreateBranch(branchName);
            repo.Checkout(newBranch);

            _logger.LogInformation("Created and switched to new branch {BranchName}", branchName);
            return Task.FromResult(GitOperationResult.SucceededWithBranch(branchName, "Created new branch"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create/switch branch for work item {WorkItemId}", workItem.Id);
            return Task.FromResult(GitOperationResult.Failed($"Failed to create/switch branch: {ex.Message}"));
        }
    }

    public Task<GitOperationResult> CommitChangesAsync(
        WorkItem workItem,
        WorkExecutionResult executionResult,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(executionResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        try
        {
            using var repo = OpenRepository(workingDirectory);

            // Check for conflicts
            var conflicts = GetConflictingFiles(repo);
            if (conflicts.Count > 0)
            {
                _logger.LogWarning(
                    "Cannot commit changes for work item {WorkItemId}: {ConflictCount} conflicts detected",
                    workItem.Id,
                    conflicts.Count);
                return Task.FromResult(GitOperationResult.FailedWithConflicts(conflicts));
            }

            // Stage all changes
            var status = repo.RetrieveStatus(new StatusOptions());
            var hasChanges = StageChanges(repo, status);

            if (!hasChanges)
            {
                _logger.LogInformation("No changes to commit for work item {WorkItemId}", workItem.Id);
                return Task.FromResult(GitOperationResult.Succeeded("No changes to commit"));
            }

            // Generate commit message and create commit
            var commitMessage = GenerateCommitMessage(workItem, executionResult);
            var signature = GetSignature(repo);

            var commit = repo.Commit(commitMessage, signature, signature);

            _logger.LogInformation(
                "Committed changes for work item {WorkItemId}: {CommitSha}",
                workItem.Id,
                commit.Sha[..7]);

            return Task.FromResult(GitOperationResult.SucceededWithCommit(commit.Sha, "Changes committed"));
        }
        catch (EmptyCommitException)
        {
            _logger.LogInformation("No changes to commit for work item {WorkItemId}", workItem.Id);
            return Task.FromResult(GitOperationResult.Succeeded("No changes to commit"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit changes for work item {WorkItemId}", workItem.Id);
            return Task.FromResult(GitOperationResult.Failed($"Failed to commit changes: {ex.Message}"));
        }
    }

    public Task<GitRepositoryStatus> GetStatusAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        try
        {
            if (!IsGitRepository(workingDirectory))
            {
                return Task.FromResult(GitRepositoryStatus.Invalid("Not a git repository"));
            }

            using var repo = OpenRepository(workingDirectory);
            var status = repo.RetrieveStatus(new StatusOptions());

            var result = new GitRepositoryStatus
            {
                IsValid = true,
                CurrentBranch = repo.Head.FriendlyName,
                HasUncommittedChanges = status.IsDirty,
                HasStagedChanges = status.Staged.Any(),
                HasUntrackedFiles = status.Untracked.Any(),
                ModifiedFiles = status.Modified.Select(e => e.FilePath).ToList(),
                StagedFiles = status.Staged.Select(e => e.FilePath).ToList(),
                UntrackedFiles = status.Untracked.Select(e => e.FilePath).ToList()
            };

            // Check tracking status if there's a remote tracking branch
            var trackingBranch = repo.Head.TrackedBranch;
            if (trackingBranch != null)
            {
                var aheadBy = repo.Head.TrackingDetails.AheadBy;
                var behindBy = repo.Head.TrackingDetails.BehindBy;

                result.IsAheadOfRemote = aheadBy > 0;
                result.CommitsAhead = aheadBy ?? 0;
                result.IsBehindRemote = behindBy > 0;
                result.CommitsBehind = behindBy ?? 0;
            }

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get repository status at {Directory}", workingDirectory);
            return Task.FromResult(GitRepositoryStatus.Invalid($"Failed to get status: {ex.Message}"));
        }
    }

    public Task<GitOperationResult> PushAsync(
        string workingDirectory,
        string remoteName = "origin",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        try
        {
            using var repo = OpenRepository(workingDirectory);

            var remote = repo.Network.Remotes[remoteName];
            if (remote == null)
            {
                _logger.LogWarning("Remote '{RemoteName}' not found", remoteName);
                return Task.FromResult(GitOperationResult.Failed($"Remote '{remoteName}' not found"));
            }

            var currentBranch = repo.Head;
            if (currentBranch.IsTracking)
            {
                // Push to existing tracking branch
                var pushRefSpec = $"+{currentBranch.CanonicalName}:{currentBranch.TrackedBranch?.CanonicalName ?? currentBranch.CanonicalName}";
                repo.Network.Push(remote, pushRefSpec);
            }
            else
            {
                // Push and set up tracking
                var pushRefSpec = $"{currentBranch.CanonicalName}:refs/heads/{currentBranch.FriendlyName}";
                repo.Network.Push(remote, pushRefSpec);
            }

            _logger.LogInformation(
                "Pushed branch {BranchName} to {RemoteName}",
                currentBranch.FriendlyName,
                remoteName);

            return Task.FromResult(GitOperationResult.Succeeded($"Pushed to {remoteName}"));
        }
        catch (LibGit2SharpException ex) when (ex.Message.Contains("authentication"))
        {
            _logger.LogWarning(ex, "Push failed: authentication required");
            return Task.FromResult(GitOperationResult.Failed("Push failed: authentication required. Please configure git credentials."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push to {RemoteName}", remoteName);
            return Task.FromResult(GitOperationResult.Failed($"Failed to push: {ex.Message}"));
        }
    }

    public string GenerateBranchName(WorkItem workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        // Use external ID if available (e.g., GitHub issue number), otherwise use internal ID
        var identifier = !string.IsNullOrEmpty(workItem.ExternalId)
            ? workItem.ExternalId
            : workItem.Id.ToString("N")[..8];

        // Sanitize title for branch name
        var sanitizedTitle = SanitizeForBranchName(workItem.Title);

        // Limit total length (git branch names should be reasonable)
        var titlePart = sanitizedTitle.Length > 40
            ? sanitizedTitle[..40].TrimEnd('-')
            : sanitizedTitle;

        return $"bartleby/{identifier}-{titlePart}";
    }

    public string GenerateCommitMessage(WorkItem workItem, WorkExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(executionResult);

        var sb = new StringBuilder();

        // First line: type and title (max 72 chars per convention)
        var type = DetermineCommitType(workItem, executionResult);
        var firstLine = $"{type}: {workItem.Title}";
        if (firstLine.Length > 72)
        {
            firstLine = firstLine[..69] + "...";
        }
        sb.AppendLine(firstLine);
        sb.AppendLine();

        // Body: summary from execution result
        if (!string.IsNullOrEmpty(executionResult.Summary))
        {
            // Wrap long lines at 72 characters
            var wrappedSummary = WrapText(executionResult.Summary, 72);
            sb.AppendLine(wrappedSummary);
            sb.AppendLine();
        }

        // Footer: external reference and modified files
        if (!string.IsNullOrEmpty(workItem.ExternalUrl))
        {
            sb.AppendLine($"Ref: {workItem.ExternalUrl}");
        }

        if (executionResult.ModifiedFiles.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Modified files:");
            foreach (var file in executionResult.ModifiedFiles.Take(10))
            {
                sb.AppendLine($"  - {file}");
            }
            if (executionResult.ModifiedFiles.Count > 10)
            {
                sb.AppendLine($"  ... and {executionResult.ModifiedFiles.Count - 10} more");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Automated commit by Bartleby");

        return sb.ToString().TrimEnd();
    }

    private IRepositoryWrapper OpenRepository(string workingDirectory)
    {
        if (_repositoryFactory != null)
        {
            return _repositoryFactory(workingDirectory);
        }

        var repoPath = Repository.Discover(workingDirectory);
        if (string.IsNullOrEmpty(repoPath))
        {
            throw new RepositoryNotFoundException($"No git repository found at {workingDirectory}");
        }

        return new RepositoryWrapper(repoPath);
    }

    private static Signature GetSignature(IRepositoryWrapper repo)
    {
        var config = repo.Config;
        var name = config.Get<string>("user.name")?.Value ?? "Bartleby";
        var email = config.Get<string>("user.email")?.Value ?? "bartleby@localhost";
        return new Signature(name, email, DateTimeOffset.Now);
    }

    private static List<string> GetConflictingFiles(IRepositoryWrapper repo)
    {
        var status = repo.RetrieveStatus(new StatusOptions());
        return status
            .Where(e => e.State.HasFlag(FileStatus.Conflicted))
            .Select(e => e.FilePath)
            .ToList();
    }

    private bool StageChanges(IRepositoryWrapper repo, RepositoryStatus status)
    {
        var filesToStage = new List<string>();

        // Stage modified files
        filesToStage.AddRange(status.Modified.Select(e => e.FilePath));

        // Stage new/untracked files
        filesToStage.AddRange(status.Untracked.Select(e => e.FilePath));

        // Stage deleted files
        filesToStage.AddRange(status.Missing.Select(e => e.FilePath));

        if (filesToStage.Count == 0)
        {
            return false;
        }

        repo.Stage(filesToStage);
        _logger.LogDebug("Staged {FileCount} files", filesToStage.Count);
        return true;
    }

    private static string DetermineCommitType(WorkItem workItem, WorkExecutionResult executionResult)
    {
        // Check labels for hints
        var labels = workItem.Labels.Select(l => l.ToLowerInvariant()).ToList();

        if (labels.Contains("bug") || labels.Contains("fix") || labels.Contains("bugfix"))
            return "fix";

        if (labels.Contains("feature") || labels.Contains("enhancement"))
            return "feat";

        if (labels.Contains("docs") || labels.Contains("documentation"))
            return "docs";

        if (labels.Contains("test") || labels.Contains("tests") || labels.Contains("testing"))
            return "test";

        if (labels.Contains("refactor") || labels.Contains("refactoring"))
            return "refactor";

        if (labels.Contains("chore") || labels.Contains("maintenance"))
            return "chore";

        // Check title keywords
        var title = workItem.Title.ToLowerInvariant();

        if (title.Contains("fix") || title.Contains("bug") || title.Contains("issue"))
            return "fix";

        if (title.Contains("add") || title.Contains("implement") || title.Contains("create"))
            return "feat";

        if (title.Contains("update") || title.Contains("refactor"))
            return "refactor";

        if (title.Contains("test"))
            return "test";

        if (title.Contains("doc"))
            return "docs";

        // Default to feat for new work
        return "feat";
    }

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleDashesRegex();

    private static string SanitizeForBranchName(string input)
    {
        // Convert to lowercase
        var result = input.ToLowerInvariant();

        // Replace spaces and underscores with dashes
        result = result.Replace(' ', '-').Replace('_', '-');

        // Remove any characters that aren't alphanumeric or dashes
        result = NonAlphanumericRegex().Replace(result, "");

        // Collapse multiple dashes into one
        result = MultipleDashesRegex().Replace(result, "-");

        // Remove leading/trailing dashes
        return result.Trim('-');
    }

    private static string WrapText(string text, int maxLineLength)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var currentLine = new StringBuilder();

        foreach (var word in words)
        {
            if (currentLine.Length + word.Length + 1 > maxLineLength && currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
                currentLine.Clear();
            }

            if (currentLine.Length > 0)
            {
                currentLine.Append(' ');
            }
            currentLine.Append(word);
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine.ToString());
        }

        return string.Join(Environment.NewLine, lines);
    }
}

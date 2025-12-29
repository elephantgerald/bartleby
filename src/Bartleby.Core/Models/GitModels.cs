namespace Bartleby.Core.Models;

/// <summary>
/// Result of a git operation.
/// </summary>
public class GitOperationResult
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The result message or error description.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Branch name (for branch operations).
    /// </summary>
    public string? BranchName { get; set; }

    /// <summary>
    /// Commit SHA (for commit operations).
    /// </summary>
    public string? CommitSha { get; set; }

    /// <summary>
    /// Whether the operation encountered a conflict.
    /// </summary>
    public bool HasConflicts { get; set; }

    /// <summary>
    /// Files that have conflicts (if any).
    /// </summary>
    public List<string> ConflictingFiles { get; set; } = [];

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static GitOperationResult Succeeded(string? message = null) => new()
    {
        Success = true,
        Message = message
    };

    /// <summary>
    /// Creates a successful result with a branch name.
    /// </summary>
    public static GitOperationResult SucceededWithBranch(string branchName, string? message = null) => new()
    {
        Success = true,
        BranchName = branchName,
        Message = message
    };

    /// <summary>
    /// Creates a successful result with a commit SHA.
    /// </summary>
    public static GitOperationResult SucceededWithCommit(string commitSha, string? message = null) => new()
    {
        Success = true,
        CommitSha = commitSha,
        Message = message
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static GitOperationResult Failed(string message) => new()
    {
        Success = false,
        Message = message
    };

    /// <summary>
    /// Creates a failed result due to conflicts.
    /// </summary>
    public static GitOperationResult FailedWithConflicts(IEnumerable<string> conflictingFiles) => new()
    {
        Success = false,
        HasConflicts = true,
        ConflictingFiles = conflictingFiles.ToList(),
        Message = $"Merge conflicts detected in {conflictingFiles.Count()} file(s)"
    };
}

/// <summary>
/// Status of a git repository.
/// </summary>
public class GitRepositoryStatus
{
    /// <summary>
    /// Whether this is a valid git repository.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// The current branch name.
    /// </summary>
    public string? CurrentBranch { get; set; }

    /// <summary>
    /// Whether there are uncommitted changes.
    /// </summary>
    public bool HasUncommittedChanges { get; set; }

    /// <summary>
    /// Whether there are staged changes.
    /// </summary>
    public bool HasStagedChanges { get; set; }

    /// <summary>
    /// Whether there are untracked files.
    /// </summary>
    public bool HasUntrackedFiles { get; set; }

    /// <summary>
    /// List of modified files.
    /// </summary>
    public List<string> ModifiedFiles { get; set; } = [];

    /// <summary>
    /// List of staged files.
    /// </summary>
    public List<string> StagedFiles { get; set; } = [];

    /// <summary>
    /// List of untracked files.
    /// </summary>
    public List<string> UntrackedFiles { get; set; } = [];

    /// <summary>
    /// Whether the branch is ahead of the remote.
    /// </summary>
    public bool IsAheadOfRemote { get; set; }

    /// <summary>
    /// Number of commits ahead of the remote.
    /// </summary>
    public int CommitsAhead { get; set; }

    /// <summary>
    /// Whether the branch is behind the remote.
    /// </summary>
    public bool IsBehindRemote { get; set; }

    /// <summary>
    /// Number of commits behind the remote.
    /// </summary>
    public int CommitsBehind { get; set; }

    /// <summary>
    /// Error message if the repository is invalid.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates an invalid repository status.
    /// </summary>
    public static GitRepositoryStatus Invalid(string errorMessage) => new()
    {
        IsValid = false,
        ErrorMessage = errorMessage
    };
}

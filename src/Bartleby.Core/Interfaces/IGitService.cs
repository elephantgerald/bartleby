using Bartleby.Core.Models;

namespace Bartleby.Core.Interfaces;

/// <summary>
/// Service for automating git operations for completed work items.
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Checks if the specified directory is within a git repository.
    /// </summary>
    /// <param name="workingDirectory">The directory to check.</param>
    /// <returns>True if the directory is in a git repository.</returns>
    bool IsGitRepository(string workingDirectory);

    /// <summary>
    /// Initializes a new git repository in the specified directory.
    /// </summary>
    /// <param name="workingDirectory">The directory to initialize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the initialization.</returns>
    Task<GitOperationResult> InitializeRepositoryAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or switches to a branch for the specified work item.
    /// Branch naming convention: bartleby/{work-item-id}
    /// </summary>
    /// <param name="workItem">The work item to create a branch for.</param>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the branch name.</returns>
    Task<GitOperationResult> CreateOrSwitchToBranchAsync(
        WorkItem workItem,
        string workingDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages and commits all changes for the specified work item.
    /// </summary>
    /// <param name="workItem">The work item providing context for the commit.</param>
    /// <param name="executionResult">The execution result containing summary and modified files.</param>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the commit SHA.</returns>
    Task<GitOperationResult> CommitChangesAsync(
        WorkItem workItem,
        WorkExecutionResult executionResult,
        string workingDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of the repository.
    /// </summary>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The repository status.</returns>
    Task<GitRepositoryStatus> GetStatusAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes the current branch to the remote repository.
    /// </summary>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="remoteName">The remote name (default: "origin").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the push operation.</returns>
    Task<GitOperationResult> PushAsync(
        string workingDirectory,
        string remoteName = "origin",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a branch name for a work item following the naming convention.
    /// </summary>
    /// <param name="workItem">The work item.</param>
    /// <returns>The branch name.</returns>
    string GenerateBranchName(WorkItem workItem);

    /// <summary>
    /// Generates a commit message for a work item based on execution results.
    /// </summary>
    /// <param name="workItem">The work item.</param>
    /// <param name="executionResult">The execution result.</param>
    /// <returns>The commit message.</returns>
    string GenerateCommitMessage(WorkItem workItem, WorkExecutionResult executionResult);
}

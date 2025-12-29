using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Bartleby.Infrastructure.Git;
using FluentAssertions;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Moq;

namespace Bartleby.Infrastructure.Tests.Git;

/// <summary>
/// Integration tests for GitService using real git repositories.
/// These tests create temporary directories with actual git repos.
/// </summary>
public class GitServiceIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly Mock<ILogger<GitService>> _loggerMock;
    private readonly GitService _sut;

    public GitServiceIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"bartleby-git-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        _loggerMock = new Mock<ILogger<GitService>>();
        _sut = new GitService(_loggerMock.Object);
    }

    public void Dispose()
    {
        // Clean up the test directory
        try
        {
            // LibGit2Sharp leaves read-only files, need to make them writable before deleting
            if (Directory.Exists(_testDirectory))
            {
                SetAttributesNormal(new DirectoryInfo(_testDirectory));
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    private static void SetAttributesNormal(DirectoryInfo directory)
    {
        foreach (var subDir in directory.GetDirectories())
        {
            SetAttributesNormal(subDir);
        }
        foreach (var file in directory.GetFiles())
        {
            file.Attributes = FileAttributes.Normal;
        }
    }

    #region IsGitRepository

    [Fact]
    public void IsGitRepository_WhenNotARepo_ReturnsFalse()
    {
        // Arrange - _testDirectory is just a regular directory

        // Act
        var result = _sut.IsGitRepository(_testDirectory);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsGitRepository_WhenIsARepo_ReturnsTrue()
    {
        // Arrange
        Repository.Init(_testDirectory);

        // Act
        var result = _sut.IsGitRepository(_testDirectory);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsGitRepository_WhenSubdirectoryOfRepo_ReturnsTrue()
    {
        // Arrange
        Repository.Init(_testDirectory);
        var subDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subDir);

        // Act
        var result = _sut.IsGitRepository(subDir);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region InitializeRepositoryAsync

    [Fact]
    public async Task InitializeRepositoryAsync_WhenNotARepo_InitializesSuccessfully()
    {
        // Act
        var result = await _sut.InitializeRepositoryAsync(_testDirectory);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("initialized");
        _sut.IsGitRepository(_testDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task InitializeRepositoryAsync_WhenAlreadyARepo_ReturnsSuccessWithExistsMessage()
    {
        // Arrange
        Repository.Init(_testDirectory);

        // Act
        var result = await _sut.InitializeRepositoryAsync(_testDirectory);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("already exists");
    }

    #endregion

    #region CreateOrSwitchToBranchAsync

    [Fact]
    public async Task CreateOrSwitchToBranchAsync_CreatesNewBranch()
    {
        // Arrange
        InitializeRepoWithInitialCommit();
        var workItem = CreateWorkItem(title: "Add feature", externalId: "123");

        // Act
        var result = await _sut.CreateOrSwitchToBranchAsync(workItem, _testDirectory);

        // Assert
        result.Success.Should().BeTrue();
        result.BranchName.Should().Be("bartleby/123-add-feature");
        result.Message.Should().Contain("Created");

        // Verify we're on the new branch
        using var repo = new Repository(_testDirectory);
        repo.Head.FriendlyName.Should().Be("bartleby/123-add-feature");
    }

    [Fact]
    public async Task CreateOrSwitchToBranchAsync_SwitchesToExistingBranch()
    {
        // Arrange
        InitializeRepoWithInitialCommit();
        var workItem = CreateWorkItem(title: "Existing feature", externalId: "456");

        // Create the branch first
        using (var repo = new Repository(_testDirectory))
        {
            repo.CreateBranch("bartleby/456-existing-feature");
        }

        // Act
        var result = await _sut.CreateOrSwitchToBranchAsync(workItem, _testDirectory);

        // Assert
        result.Success.Should().BeTrue();
        result.BranchName.Should().Be("bartleby/456-existing-feature");
        result.Message.Should().Contain("existing");
    }

    #endregion

    #region CommitChangesAsync

    [Fact]
    public async Task CommitChangesAsync_WithChanges_CommitsSuccessfully()
    {
        // Arrange
        InitializeRepoWithInitialCommit();
        var workItem = CreateWorkItem(title: "Add feature", labels: ["feature"]);
        var executionResult = CreateExecutionResult(summary: "Added new functionality");

        // Create a new file to commit
        var newFile = Path.Combine(_testDirectory, "newfile.txt");
        await File.WriteAllTextAsync(newFile, "Hello, World!");

        // Act
        var result = await _sut.CommitChangesAsync(workItem, executionResult, _testDirectory);

        // Assert
        result.Success.Should().BeTrue();
        result.CommitSha.Should().NotBeNullOrEmpty();
        result.Message.Should().Contain("committed");

        // Verify commit exists
        using var repo = new Repository(_testDirectory);
        var commit = repo.Head.Tip;
        commit.Message.Should().Contain("feat: Add feature");
        commit.Message.Should().Contain("Added new functionality");
    }

    [Fact]
    public async Task CommitChangesAsync_WithNoChanges_ReturnsSuccessNoChanges()
    {
        // Arrange
        InitializeRepoWithInitialCommit();
        var workItem = CreateWorkItem();
        var executionResult = CreateExecutionResult();

        // No changes made

        // Act
        var result = await _sut.CommitChangesAsync(workItem, executionResult, _testDirectory);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("No changes");
    }

    [Fact]
    public async Task CommitChangesAsync_WithModifiedFile_CommitsModification()
    {
        // Arrange
        InitializeRepoWithInitialCommit();
        var workItem = CreateWorkItem(title: "Fix bug", labels: ["bug"]);
        var executionResult = CreateExecutionResult(summary: "Fixed null reference");

        // Modify an existing file
        var existingFile = Path.Combine(_testDirectory, "README.md");
        await File.WriteAllTextAsync(existingFile, "Modified content");

        // Act
        var result = await _sut.CommitChangesAsync(workItem, executionResult, _testDirectory);

        // Assert
        result.Success.Should().BeTrue();
        result.CommitSha.Should().NotBeNullOrEmpty();

        // Verify commit message uses "fix" type
        using var repo = new Repository(_testDirectory);
        var commit = repo.Head.Tip;
        commit.Message.Should().StartWith("fix: Fix bug");
    }

    #endregion

    #region GetStatusAsync

    [Fact]
    public async Task GetStatusAsync_OnCleanRepo_ReturnsCleanStatus()
    {
        // Arrange
        InitializeRepoWithInitialCommit();

        // Act
        var status = await _sut.GetStatusAsync(_testDirectory);

        // Assert
        status.IsValid.Should().BeTrue();
        status.CurrentBranch.Should().Be("main");
        status.HasUncommittedChanges.Should().BeFalse();
        status.HasStagedChanges.Should().BeFalse();
        status.HasUntrackedFiles.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_WithModifiedFile_ReportsModification()
    {
        // Arrange
        InitializeRepoWithInitialCommit();
        var file = Path.Combine(_testDirectory, "README.md");
        await File.WriteAllTextAsync(file, "Modified");

        // Act
        var status = await _sut.GetStatusAsync(_testDirectory);

        // Assert
        status.IsValid.Should().BeTrue();
        status.HasUncommittedChanges.Should().BeTrue();
        status.ModifiedFiles.Should().Contain("README.md");
    }

    [Fact]
    public async Task GetStatusAsync_WithUntrackedFile_ReportsUntracked()
    {
        // Arrange
        InitializeRepoWithInitialCommit();
        var file = Path.Combine(_testDirectory, "newfile.txt");
        await File.WriteAllTextAsync(file, "New file");

        // Act
        var status = await _sut.GetStatusAsync(_testDirectory);

        // Assert
        status.IsValid.Should().BeTrue();
        status.HasUntrackedFiles.Should().BeTrue();
        status.UntrackedFiles.Should().Contain("newfile.txt");
    }

    [Fact]
    public async Task GetStatusAsync_WhenNotARepo_ReturnsInvalid()
    {
        // Arrange - don't initialize as repo

        // Act
        var status = await _sut.GetStatusAsync(_testDirectory);

        // Assert
        status.IsValid.Should().BeFalse();
        status.ErrorMessage.Should().Contain("Not a git repository");
    }

    #endregion

    #region Helper Methods

    private void InitializeRepoWithInitialCommit()
    {
        Repository.Init(_testDirectory);

        // Create initial commit (required for branch operations)
        using var repo = new Repository(_testDirectory);

        // Set user config for commits
        repo.Config.Set("user.name", "Bartleby Test");
        repo.Config.Set("user.email", "test@bartleby.local");

        // Create a file and commit it
        var readmePath = Path.Combine(_testDirectory, "README.md");
        File.WriteAllText(readmePath, "# Test Repository");
        Commands.Stage(repo, "README.md");

        var signature = new Signature("Bartleby Test", "test@bartleby.local", DateTimeOffset.Now);
        repo.Commit("Initial commit", signature, signature);

        // Rename default branch to "main" for consistency
        var mainBranch = repo.Branches["master"] ?? repo.Branches["main"];
        if (mainBranch != null && mainBranch.FriendlyName == "master")
        {
            Commands.Checkout(repo, repo.Branches.Rename("master", "main"));
        }
    }

    private static WorkItem CreateWorkItem(
        string title = "Test Work Item",
        string? externalId = "42",
        List<string>? labels = null)
    {
        return new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "Test description",
            Status = WorkItemStatus.Ready,
            ExternalId = externalId,
            ExternalUrl = "https://github.com/test/repo/issues/1",
            Labels = labels ?? ["story"]
        };
    }

    private static WorkExecutionResult CreateExecutionResult(
        string summary = "Work completed",
        List<string>? modifiedFiles = null)
    {
        return new WorkExecutionResult
        {
            Success = true,
            Outcome = WorkExecutionOutcome.Completed,
            Summary = summary,
            ModifiedFiles = modifiedFiles ?? []
        };
    }

    #endregion
}

using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Bartleby.Infrastructure.Git;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Moq;

namespace Bartleby.Infrastructure.Tests.Git;

public class GitServiceTests
{
    private readonly Mock<ILogger<GitService>> _loggerMock;
    private readonly Mock<IRepositoryWrapper> _repositoryMock;
    private readonly GitService _sut;

    public GitServiceTests()
    {
        _loggerMock = new Mock<ILogger<GitService>>();
        _repositoryMock = new Mock<IRepositoryWrapper>();

        _sut = new GitService(
            _loggerMock.Object,
            _ => _repositoryMock.Object);
    }

    #region GenerateBranchName

    [Fact]
    public void GenerateBranchName_WithExternalId_UsesBartlebyPrefixAndExternalId()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Add login feature", externalId: "123");

        // Act
        var branchName = _sut.GenerateBranchName(workItem);

        // Assert
        Assert.Equal("bartleby/123-add-login-feature", branchName);
    }

    [Fact]
    public void GenerateBranchName_WithoutExternalId_UsesShortGuidPrefix()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Fix bug");
        workItem.ExternalId = null;

        // Act
        var branchName = _sut.GenerateBranchName(workItem);

        // Assert
        Assert.StartsWith("bartleby/", branchName);
        Assert.EndsWith("-fix-bug", branchName);
    }

    [Fact]
    public void GenerateBranchName_WithSpecialCharacters_SanitizesTitle()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Fix: bug #123 (urgent!) @team", externalId: "456");

        // Act
        var branchName = _sut.GenerateBranchName(workItem);

        // Assert
        Assert.Equal("bartleby/456-fix-bug-123-urgent-team", branchName);
    }

    [Fact]
    public void GenerateBranchName_WithLongTitle_TruncatesTo40Characters()
    {
        // Arrange
        var longTitle = "This is a very long title that should be truncated to keep the branch name reasonable";
        var workItem = CreateWorkItem(title: longTitle, externalId: "789");

        // Act
        var branchName = _sut.GenerateBranchName(workItem);

        // Assert
        // "bartleby/789-" = 13 chars, title part should be 40 chars max
        Assert.True(branchName.Length <= 13 + 40);
        Assert.StartsWith("bartleby/789-", branchName);
    }

    [Fact]
    public void GenerateBranchName_WhenWorkItemIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _sut.GenerateBranchName(null!));
    }

    #endregion

    #region GenerateCommitMessage

    [Fact]
    public void GenerateCommitMessage_WithBugLabel_UsesFixType()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Null reference exception in login", labels: ["bug"]);
        var result = CreateExecutionResult(summary: "Fixed null check in authentication flow");

        // Act
        var message = _sut.GenerateCommitMessage(workItem, result);

        // Assert
        Assert.StartsWith("fix: Null reference exception in login", message);
        Assert.Contains("Fixed null check in authentication flow", message);
    }

    [Fact]
    public void GenerateCommitMessage_WithFeatureLabel_UsesFeatType()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Add user dashboard", labels: ["feature"]);
        var result = CreateExecutionResult(summary: "Implemented dashboard page");

        // Act
        var message = _sut.GenerateCommitMessage(workItem, result);

        // Assert
        Assert.StartsWith("feat: Add user dashboard", message);
    }

    [Fact]
    public void GenerateCommitMessage_WithDocsLabel_UsesDocsType()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Update README", labels: ["docs"]);
        var result = CreateExecutionResult(summary: "Added installation instructions");

        // Act
        var message = _sut.GenerateCommitMessage(workItem, result);

        // Assert
        Assert.StartsWith("docs: Update README", message);
    }

    [Fact]
    public void GenerateCommitMessage_WithTestLabel_UsesTestType()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Add unit tests for auth", labels: ["test"]);
        var result = CreateExecutionResult(summary: "Added 10 new tests");

        // Act
        var message = _sut.GenerateCommitMessage(workItem, result);

        // Assert
        Assert.StartsWith("test: Add unit tests for auth", message);
    }

    [Fact]
    public void GenerateCommitMessage_WithNoLabels_InfersTypeFromTitle()
    {
        // Arrange - title contains "fix"
        var workItem = CreateWorkItem(title: "Fix login issue", labels: []);
        var result = CreateExecutionResult(summary: "Fixed the bug");

        // Act
        var message = _sut.GenerateCommitMessage(workItem, result);

        // Assert
        Assert.StartsWith("fix: Fix login issue", message);
    }

    [Fact]
    public void GenerateCommitMessage_WithAddInTitle_UsesFeatType()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Add new feature", labels: []);
        var result = CreateExecutionResult();

        // Act
        var message = _sut.GenerateCommitMessage(workItem, result);

        // Assert
        Assert.StartsWith("feat: Add new feature", message);
    }

    [Fact]
    public void GenerateCommitMessage_IncludesExternalUrl()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Fix bug", externalUrl: "https://github.com/test/repo/issues/42");
        var result = CreateExecutionResult(summary: "Fixed");

        // Act
        var message = _sut.GenerateCommitMessage(workItem, result);

        // Assert
        Assert.Contains("Ref: https://github.com/test/repo/issues/42", message);
    }

    [Fact]
    public void GenerateCommitMessage_IncludesModifiedFiles()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Refactor code");
        var result = CreateExecutionResult(
            summary: "Refactored",
            modifiedFiles: ["src/Service.cs", "src/Controller.cs"]);

        // Act
        var message = _sut.GenerateCommitMessage(workItem, result);

        // Assert
        Assert.Contains("Modified files:", message);
        Assert.Contains("- src/Service.cs", message);
        Assert.Contains("- src/Controller.cs", message);
    }

    [Fact]
    public void GenerateCommitMessage_WithManyFiles_TruncatesTo10()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Large refactor");
        var files = Enumerable.Range(1, 15).Select(i => $"file{i}.cs").ToList();
        var result = CreateExecutionResult(summary: "Done", modifiedFiles: files);

        // Act
        var message = _sut.GenerateCommitMessage(workItem, result);

        // Assert
        Assert.Contains("file10.cs", message);
        Assert.DoesNotContain("file11.cs", message);
        Assert.Contains("... and 5 more", message);
    }

    [Fact]
    public void GenerateCommitMessage_IncludesBartlebyFooter()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Any work");
        var result = CreateExecutionResult();

        // Act
        var message = _sut.GenerateCommitMessage(workItem, result);

        // Assert
        Assert.Contains("Automated commit by Bartleby", message);
    }

    [Fact]
    public void GenerateCommitMessage_TruncatesLongTitleTo72Chars()
    {
        // Arrange
        var longTitle = "This is a very very very very very very very long title that exceeds seventy two characters limit";
        var workItem = CreateWorkItem(title: longTitle);
        var result = CreateExecutionResult();

        // Act
        var message = _sut.GenerateCommitMessage(workItem, result);

        // Assert
        var firstLine = message.Split('\n')[0].TrimEnd('\r');
        Assert.True(firstLine.Length <= 72);
        Assert.EndsWith("...", firstLine);
    }

    #endregion

    #region CreateOrSwitchToBranchAsync

    [Fact]
    public async Task CreateOrSwitchToBranchAsync_WhenBranchDoesNotExist_CreatesAndSwitchesToNewBranch()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "New feature", externalId: "100");
        var expectedBranchName = "bartleby/100-new-feature";

        var branchesMock = new Mock<BranchCollection>();
        branchesMock
            .Setup(b => b[expectedBranchName])
            .Returns((Branch)null!);

        var newBranchMock = new Mock<Branch>();
        newBranchMock.Setup(b => b.FriendlyName).Returns(expectedBranchName);

        var headMock = new Mock<Branch>();
        headMock.Setup(h => h.FriendlyName).Returns("main");

        _repositoryMock.Setup(r => r.Branches).Returns(branchesMock.Object);
        _repositoryMock.Setup(r => r.Head).Returns(headMock.Object);
        _repositoryMock.Setup(r => r.CreateBranch(expectedBranchName)).Returns(newBranchMock.Object);
        _repositoryMock.Setup(r => r.Checkout(newBranchMock.Object, null)).Returns(newBranchMock.Object);

        // Act
        var result = await _sut.CreateOrSwitchToBranchAsync(workItem, "/work");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedBranchName, result.BranchName);
        Assert.Contains("Created", result.Message);
        _repositoryMock.Verify(r => r.CreateBranch(expectedBranchName), Times.Once);
        _repositoryMock.Verify(r => r.Checkout(newBranchMock.Object, null), Times.Once);
    }

    [Fact]
    public async Task CreateOrSwitchToBranchAsync_WhenBranchExists_SwitchesToExistingBranch()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Existing feature", externalId: "200");
        var expectedBranchName = "bartleby/200-existing-feature";

        var existingBranchMock = new Mock<Branch>();
        existingBranchMock.Setup(b => b.FriendlyName).Returns(expectedBranchName);

        var branchesMock = new Mock<BranchCollection>();
        branchesMock
            .Setup(b => b[expectedBranchName])
            .Returns(existingBranchMock.Object);

        var headMock = new Mock<Branch>();
        headMock.Setup(h => h.FriendlyName).Returns("main");

        _repositoryMock.Setup(r => r.Branches).Returns(branchesMock.Object);
        _repositoryMock.Setup(r => r.Head).Returns(headMock.Object);
        _repositoryMock.Setup(r => r.Checkout(existingBranchMock.Object, null)).Returns(existingBranchMock.Object);

        // Act
        var result = await _sut.CreateOrSwitchToBranchAsync(workItem, "/work");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedBranchName, result.BranchName);
        Assert.Contains("existing", result.Message);
        _repositoryMock.Verify(r => r.CreateBranch(It.IsAny<string>()), Times.Never);
        _repositoryMock.Verify(r => r.Checkout(existingBranchMock.Object, null), Times.Once);
    }

    [Fact]
    public async Task CreateOrSwitchToBranchAsync_WhenAlreadyOnBranch_DoesNotCheckout()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Current feature", externalId: "300");
        var expectedBranchName = "bartleby/300-current-feature";

        var existingBranchMock = new Mock<Branch>();
        existingBranchMock.Setup(b => b.FriendlyName).Returns(expectedBranchName);

        var branchesMock = new Mock<BranchCollection>();
        branchesMock
            .Setup(b => b[expectedBranchName])
            .Returns(existingBranchMock.Object);

        var headMock = new Mock<Branch>();
        headMock.Setup(h => h.FriendlyName).Returns(expectedBranchName); // Already on this branch

        _repositoryMock.Setup(r => r.Branches).Returns(branchesMock.Object);
        _repositoryMock.Setup(r => r.Head).Returns(headMock.Object);

        // Act
        var result = await _sut.CreateOrSwitchToBranchAsync(workItem, "/work");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedBranchName, result.BranchName);
        _repositoryMock.Verify(r => r.Checkout(It.IsAny<Branch>(), It.IsAny<CheckoutOptions>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrSwitchToBranchAsync_WhenWorkItemIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.CreateOrSwitchToBranchAsync(null!, "/work"));
    }

    [Fact]
    public async Task CreateOrSwitchToBranchAsync_WhenDirectoryIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var workItem = CreateWorkItem();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateOrSwitchToBranchAsync(workItem, ""));
    }

    [Fact]
    public async Task CreateOrSwitchToBranchAsync_WhenExceptionThrown_ReturnsFailedResult()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Error feature", externalId: "400");
        var branchesMock = new Mock<BranchCollection>();
        branchesMock
            .Setup(b => b[It.IsAny<string>()])
            .Throws(new LibGit2SharpException("Repository error"));

        _repositoryMock.Setup(r => r.Branches).Returns(branchesMock.Object);

        // Act
        var result = await _sut.CreateOrSwitchToBranchAsync(workItem, "/work");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Repository error", result.Message);
    }

    #endregion

    #region CommitChangesAsync

    [Fact]
    public async Task CommitChangesAsync_WhenChangesExist_CommitsSuccessfully()
    {
        // Arrange
        var workItem = CreateWorkItem(title: "Completed work");
        var executionResult = CreateExecutionResult(summary: "Implemented feature");

        SetupRepositoryForCommit(hasChanges: true, commitSha: "abc123def");

        // Act
        var result = await _sut.CommitChangesAsync(workItem, executionResult, "/work");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("abc123def", result.CommitSha);
        Assert.Contains("committed", result.Message);
    }

    [Fact]
    public async Task CommitChangesAsync_WhenNoChanges_ReturnsSuccessWithNoChangesMessage()
    {
        // Arrange
        var workItem = CreateWorkItem();
        var executionResult = CreateExecutionResult();

        SetupRepositoryForCommit(hasChanges: false);

        // Act
        var result = await _sut.CommitChangesAsync(workItem, executionResult, "/work");

        // Assert
        Assert.True(result.Success);
        Assert.Contains("No changes", result.Message);
    }

    [Fact]
    public async Task CommitChangesAsync_WhenConflictsExist_ReturnsFailedWithConflicts()
    {
        // Arrange
        var workItem = CreateWorkItem();
        var executionResult = CreateExecutionResult();

        // Setup status entries that include conflicts
        var conflictingEntries = new List<StatusEntry>
        {
            CreateStatusEntryWithState("file1.cs", FileStatus.Conflicted),
            CreateStatusEntryWithState("file2.cs", FileStatus.Conflicted)
        };

        var statusMock = new Mock<RepositoryStatus>();
        // Make the enumerator return our conflict entries
        statusMock
            .As<IEnumerable<StatusEntry>>()
            .Setup(s => s.GetEnumerator())
            .Returns(conflictingEntries.GetEnumerator());

        _repositoryMock
            .Setup(r => r.RetrieveStatus(It.IsAny<StatusOptions>()))
            .Returns(statusMock.Object);

        // Act
        var result = await _sut.CommitChangesAsync(workItem, executionResult, "/work");

        // Assert
        Assert.False(result.Success);
        Assert.True(result.HasConflicts);
        Assert.Equal(2, result.ConflictingFiles.Count);
        Assert.Contains("file1.cs", result.ConflictingFiles);
    }

    [Fact]
    public async Task CommitChangesAsync_WhenWorkItemIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.CommitChangesAsync(null!, CreateExecutionResult(), "/work"));
    }

    [Fact]
    public async Task CommitChangesAsync_WhenExecutionResultIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.CommitChangesAsync(CreateWorkItem(), null!, "/work"));
    }

    #endregion

    #region GetStatusAsync

    // Note: GetStatusAsync tests that require mocking IsGitRepository are in integration tests
    // because IsGitRepository uses static Repository.Discover() which cannot be mocked

    [Fact]
    public async Task GetStatusAsync_WhenDirectoryIsEmpty_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.GetStatusAsync(""));
    }

    [Fact]
    public async Task GetStatusAsync_WhenNotAGitRepository_ReturnsInvalidStatus()
    {
        // Arrange - the mock factory won't be used because IsGitRepository returns false first
        // This tests the early return path

        // Act
        var status = await _sut.GetStatusAsync("/nonexistent/path");

        // Assert
        Assert.False(status.IsValid);
        Assert.Contains("Not a git repository", status.ErrorMessage);
    }

    #endregion

    #region PushAsync

    [Fact]
    public async Task PushAsync_WhenRemoteExists_PushesSuccessfully()
    {
        // Arrange
        var remoteMock = new Mock<Remote>();
        var remotesMock = new Mock<RemoteCollection>();
        remotesMock.Setup(r => r["origin"]).Returns(remoteMock.Object);

        var headMock = new Mock<Branch>();
        headMock.Setup(h => h.FriendlyName).Returns("feature-branch");
        headMock.Setup(h => h.CanonicalName).Returns("refs/heads/feature-branch");
        headMock.Setup(h => h.IsTracking).Returns(false);

        var networkMock = new Mock<Network>();
        networkMock.Setup(n => n.Remotes).Returns(remotesMock.Object);

        _repositoryMock.Setup(r => r.Head).Returns(headMock.Object);
        _repositoryMock.Setup(r => r.Network).Returns(networkMock.Object);

        // Act
        var result = await _sut.PushAsync("/work");

        // Assert
        Assert.True(result.Success);
        Assert.Contains("origin", result.Message);
    }

    [Fact]
    public async Task PushAsync_WhenRemoteNotFound_ReturnsFailedResult()
    {
        // Arrange
        var remotesMock = new Mock<RemoteCollection>();
        remotesMock.Setup(r => r["nonexistent"]).Returns((Remote)null!);

        var networkMock = new Mock<Network>();
        networkMock.Setup(n => n.Remotes).Returns(remotesMock.Object);

        _repositoryMock.Setup(r => r.Network).Returns(networkMock.Object);

        // Act
        var result = await _sut.PushAsync("/work", "nonexistent");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message);
    }

    [Fact]
    public async Task PushAsync_WhenDirectoryIsEmpty_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.PushAsync(""));
    }

    #endregion

    #region Helper Methods

    private static WorkItem CreateWorkItem(
        string title = "Test Work Item",
        string? externalId = "42",
        string? externalUrl = null,
        List<string>? labels = null)
    {
        return new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "Test description",
            Status = WorkItemStatus.Ready,
            ExternalId = externalId,
            ExternalUrl = externalUrl ?? "https://github.com/test/repo/issues/1",
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

    private void SetupRepositoryForCommit(bool hasChanges, string commitSha = "abc123")
    {
        // Setup status with no conflicts
        var statusEntries = new List<StatusEntry>();
        if (hasChanges)
        {
            // Add a modified file entry
        }

        var statusMock = new Mock<RepositoryStatus>();
        statusMock.Setup(s => s.IsDirty).Returns(hasChanges);
        statusMock.Setup(s => s.Staged).Returns([]);
        statusMock.Setup(s => s.Modified).Returns(hasChanges ? CreateStatusEntries("modified.cs") : []);
        statusMock.Setup(s => s.Untracked).Returns([]);
        statusMock.Setup(s => s.Missing).Returns([]);
        statusMock.Setup(s => s.GetEnumerator()).Returns(statusEntries.GetEnumerator());

        _repositoryMock
            .Setup(r => r.RetrieveStatus(It.IsAny<StatusOptions>()))
            .Returns(statusMock.Object);

        var configMock = new Mock<Configuration>();
        var configEntry = CreateConfigEntry("user.name", "Bartleby");
        var emailEntry = CreateConfigEntry("user.email", "bartleby@test.com");

        configMock.Setup(c => c.Get<string>("user.name")).Returns(configEntry!);
        configMock.Setup(c => c.Get<string>("user.email")).Returns(emailEntry!);

        _repositoryMock.Setup(r => r.Config).Returns(configMock.Object);

        if (hasChanges)
        {
            var commitMock = new Mock<Commit>();
            commitMock.Setup(c => c.Sha).Returns(commitSha);

            _repositoryMock
                .Setup(r => r.Commit(It.IsAny<string>(), It.IsAny<Signature>(), It.IsAny<Signature>(), It.IsAny<CommitOptions>()))
                .Returns(commitMock.Object);
        }
    }

    private void SetupRepositoryWithConflicts(List<string> conflictingFiles)
    {
        var statusEntries = conflictingFiles
            .Select(f => CreateStatusEntryWithState(f, FileStatus.Conflicted))
            .ToList();

        var statusMock = new Mock<RepositoryStatus>();
        statusMock.Setup(s => s.GetEnumerator()).Returns(statusEntries.GetEnumerator());
        statusMock
            .Setup(s => s.Where(It.IsAny<Func<StatusEntry, bool>>()))
            .Returns((Func<StatusEntry, bool> predicate) => statusEntries.Where(predicate));

        _repositoryMock
            .Setup(r => r.RetrieveStatus(It.IsAny<StatusOptions>()))
            .Returns(statusMock.Object);
    }

    private void SetupRepositoryStatus(
        string currentBranch,
        bool hasModified,
        bool hasStaged,
        bool hasUntracked)
    {
        var headMock = new Mock<Branch>();
        headMock.Setup(h => h.FriendlyName).Returns(currentBranch);
        headMock.Setup(h => h.TrackedBranch).Returns((Branch)null!);

        var statusMock = new Mock<RepositoryStatus>();
        statusMock.Setup(s => s.IsDirty).Returns(hasModified || hasStaged || hasUntracked);
        statusMock.Setup(s => s.Staged).Returns(hasStaged ? CreateStatusEntries("staged.cs") : []);
        statusMock.Setup(s => s.Modified).Returns(hasModified ? CreateStatusEntries("modified.cs") : []);
        statusMock.Setup(s => s.Untracked).Returns(hasUntracked ? CreateStatusEntries("untracked.cs") : []);

        _repositoryMock.Setup(r => r.Head).Returns(headMock.Object);
        _repositoryMock
            .Setup(r => r.RetrieveStatus(It.IsAny<StatusOptions>()))
            .Returns(statusMock.Object);
    }

    private static IEnumerable<StatusEntry> CreateStatusEntries(params string[] filePaths)
    {
        return filePaths.Select(f => CreateStatusEntryWithState(f, FileStatus.ModifiedInWorkdir));
    }

    private static StatusEntry CreateStatusEntryWithState(string filePath, FileStatus state)
    {
        var entryMock = new Mock<StatusEntry>();
        entryMock.Setup(e => e.FilePath).Returns(filePath);
        entryMock.Setup(e => e.State).Returns(state);
        return entryMock.Object;
    }

    private static ConfigurationEntry<string>? CreateConfigEntry(string key, string value)
    {
        // ConfigurationEntry is tricky to mock, return null and let service use defaults
        return null;
    }

    #endregion
}

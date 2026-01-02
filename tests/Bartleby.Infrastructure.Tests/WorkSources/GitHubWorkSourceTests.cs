using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Bartleby.Infrastructure.WorkSources;
using Moq;

namespace Bartleby.Infrastructure.Tests.WorkSources;

public class GitHubWorkSourceTests
{
    private readonly Mock<ISettingsRepository> _settingsRepositoryMock;
    private readonly Mock<IGitHubApiClient> _gitHubApiClientMock;
    private readonly AppSettings _defaultSettings;
    private readonly GitHubWorkSource _sut;

    public GitHubWorkSourceTests()
    {
        _settingsRepositoryMock = new Mock<ISettingsRepository>();
        _gitHubApiClientMock = new Mock<IGitHubApiClient>();

        _defaultSettings = new AppSettings
        {
            GitHubToken = "test-token",
            GitHubOwner = "test-owner",
            GitHubRepo = "test-repo"
        };

        _settingsRepositoryMock
            .Setup(r => r.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_defaultSettings);

        _sut = new GitHubWorkSource(
            _settingsRepositoryMock.Object,
            _ => _gitHubApiClientMock.Object);
    }

    #region Name Property

    [Fact]
    public void Name_ReturnsGitHub()
    {
        Assert.Equal("GitHub", _sut.Name);
    }

    #endregion

    #region SyncAsync Tests

    [Fact]
    public async Task SyncAsync_WhenOwnerNotConfigured_ReturnsEmptyList()
    {
        // Arrange
        _defaultSettings.GitHubOwner = null;

        // Act
        var result = await _sut.SyncAsync();

        // Assert
        Assert.Empty(result);
        _gitHubApiClientMock.Verify(
            c => c.GetIssuesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncAsync_WhenRepoNotConfigured_ReturnsEmptyList()
    {
        // Arrange
        _defaultSettings.GitHubRepo = null;

        // Act
        var result = await _sut.SyncAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SyncAsync_WithNoIssues_ReturnsEmptyList()
    {
        // Arrange
        _gitHubApiClientMock
            .Setup(c => c.GetIssuesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubIssue>());

        // Act
        var result = await _sut.SyncAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SyncAsync_WithSingleIssue_MapsCorrectly()
    {
        // Arrange
        var issue = CreateIssue(1, "Test Issue", "Test body");

        _gitHubApiClientMock
            .Setup(c => c.GetIssuesAsync(_defaultSettings.GitHubOwner!, _defaultSettings.GitHubRepo!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubIssue> { issue });

        // Act
        var result = await _sut.SyncAsync();

        // Assert
        var workItems = result.ToList();
        Assert.Equal(1, workItems.Count);

        var workItem = workItems[0];
        Assert.Equal("Test Issue", workItem.Title);
        Assert.Equal("Test body", workItem.Description);
        Assert.Equal("1", workItem.ExternalId);
        Assert.Equal("GitHub", workItem.Source);
        Assert.Equal("https://github.com/test-owner/test-repo/issues/1", workItem.ExternalUrl);
    }

    [Fact]
    public async Task SyncAsync_WithMultipleIssues_MapsAllCorrectly()
    {
        // Arrange
        var issues = new List<GitHubIssue>
        {
            CreateIssue(1, "Issue 1", "Body 1"),
            CreateIssue(2, "Issue 2", "Body 2"),
            CreateIssue(3, "Issue 3", "Body 3")
        };

        _gitHubApiClientMock
            .Setup(c => c.GetIssuesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(issues);

        // Act
        var result = await _sut.SyncAsync();

        // Assert
        var resultList = result.ToList();
        Assert.Equal(3, resultList.Count);
        var externalIds = resultList.Select(w => w.ExternalId).ToList();
        Assert.Contains("1", externalIds);
        Assert.Contains("2", externalIds);
        Assert.Contains("3", externalIds);
    }

    [Fact]
    public async Task SyncAsync_SkipsPullRequests()
    {
        // Arrange
        var issue = CreateIssue(1, "Regular Issue", "Body");
        var pullRequest = CreateIssue(2, "Pull Request", "PR Body", isPullRequest: true);

        _gitHubApiClientMock
            .Setup(c => c.GetIssuesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubIssue> { issue, pullRequest });

        // Act
        var result = await _sut.SyncAsync();

        // Assert
        var resultList = result.ToList();
        Assert.Equal(1, resultList.Count);
        Assert.Equal("1", resultList.First().ExternalId);
    }

    [Fact]
    public async Task SyncAsync_GeneratesConsistentGuids()
    {
        // Arrange
        var issue = CreateIssue(42, "Test Issue", "Body");

        _gitHubApiClientMock
            .Setup(c => c.GetIssuesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubIssue> { issue });

        // Act
        var result1 = await _sut.SyncAsync();
        var result2 = await _sut.SyncAsync();

        // Assert
        Assert.Equal(result2.First().Id, result1.First().Id);
    }

    [Fact]
    public async Task SyncAsync_MapsLabelsCorrectly()
    {
        // Arrange
        var issue = CreateIssue(1, "Test Issue", "Body", labels: ["bug", "high-priority"]);

        _gitHubApiClientMock
            .Setup(c => c.GetIssuesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubIssue> { issue });

        // Act
        var result = await _sut.SyncAsync();

        // Assert
        var labels = result.First().Labels;
        Assert.Contains("bug", labels);
        Assert.Contains("high-priority", labels);
        Assert.Equal(2, labels.Count);
    }

    #endregion

    #region Label to Status Mapping Tests

    [Theory]
    [InlineData("bartleby:ready", WorkItemStatus.Ready)]
    [InlineData("ready", WorkItemStatus.Ready)]
    [InlineData("bartleby:in-progress", WorkItemStatus.InProgress)]
    [InlineData("in progress", WorkItemStatus.InProgress)]
    [InlineData("bartleby:blocked", WorkItemStatus.Blocked)]
    [InlineData("blocked", WorkItemStatus.Blocked)]
    [InlineData("bartleby:failed", WorkItemStatus.Failed)]
    [InlineData("failed", WorkItemStatus.Failed)]
    public async Task SyncAsync_MapsLabelToCorrectStatus(string labelName, WorkItemStatus expectedStatus)
    {
        // Arrange
        var issue = CreateIssue(1, "Test Issue", "Body", labels: [labelName]);

        _gitHubApiClientMock
            .Setup(c => c.GetIssuesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubIssue> { issue });

        // Act
        var result = await _sut.SyncAsync();

        // Assert
        Assert.Equal(expectedStatus, result.First().Status);
    }

    [Fact]
    public async Task SyncAsync_WithNoStatusLabels_DefaultsToPending()
    {
        // Arrange
        var issue = CreateIssue(1, "Test Issue", "Body", labels: ["bug", "feature"]);

        _gitHubApiClientMock
            .Setup(c => c.GetIssuesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubIssue> { issue });

        // Act
        var result = await _sut.SyncAsync();

        // Assert
        Assert.Equal(WorkItemStatus.Pending, result.First().Status);
    }

    [Fact]
    public async Task SyncAsync_WithNoLabels_DefaultsToPending()
    {
        // Arrange
        var issue = CreateIssue(1, "Test Issue", "Body");

        _gitHubApiClientMock
            .Setup(c => c.GetIssuesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubIssue> { issue });

        // Act
        var result = await _sut.SyncAsync();

        // Assert
        Assert.Equal(WorkItemStatus.Pending, result.First().Status);
    }

    [Fact]
    public async Task SyncAsync_WithMixedCaseLabels_MapsCorrectly()
    {
        // Arrange
        var issue = CreateIssue(1, "Test Issue", "Body", labels: ["BARTLEBY:READY"]);

        _gitHubApiClientMock
            .Setup(c => c.GetIssuesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubIssue> { issue });

        // Act
        var result = await _sut.SyncAsync();

        // Assert
        Assert.Equal(WorkItemStatus.Ready, result.First().Status);
    }

    #endregion

    #region UpdateStatusAsync Tests

    [Fact]
    public async Task UpdateStatusAsync_WhenExternalIdMissing_ThrowsArgumentException()
    {
        // Arrange
        var workItem = new WorkItem { ExternalId = null };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateStatusAsync(workItem));
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenExternalIdNotNumber_ThrowsArgumentException()
    {
        // Arrange
        var workItem = new WorkItem { ExternalId = "not-a-number" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateStatusAsync(workItem));
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenOwnerNotConfigured_ThrowsInvalidOperationException()
    {
        // Arrange
        _defaultSettings.GitHubOwner = null;
        var workItem = new WorkItem { ExternalId = "1" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateStatusAsync(workItem));
    }

    [Fact]
    public async Task UpdateStatusAsync_WithCompleteStatus_ClosesIssue()
    {
        // Arrange
        var workItem = new WorkItem
        {
            ExternalId = "42",
            Status = WorkItemStatus.Complete,
            Labels = ["bug"]
        };

        GitHubIssueUpdate? capturedUpdate = null;
        _gitHubApiClientMock
            .Setup(c => c.UpdateIssueAsync(
                _defaultSettings.GitHubOwner!,
                _defaultSettings.GitHubRepo!,
                42,
                It.IsAny<GitHubIssueUpdate>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, GitHubIssueUpdate, CancellationToken>((o, r, n, u, ct) => capturedUpdate = u)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateStatusAsync(workItem);

        // Assert
        Assert.NotNull(capturedUpdate);
        Assert.True(capturedUpdate!.IsClosed);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithInProgressStatus_AddsLabel()
    {
        // Arrange
        var workItem = new WorkItem
        {
            ExternalId = "42",
            Status = WorkItemStatus.InProgress,
            Labels = []
        };

        GitHubIssueUpdate? capturedUpdate = null;
        _gitHubApiClientMock
            .Setup(c => c.UpdateIssueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                42,
                It.IsAny<GitHubIssueUpdate>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, GitHubIssueUpdate, CancellationToken>((o, r, n, u, ct) => capturedUpdate = u)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateStatusAsync(workItem);

        // Assert
        Assert.NotNull(capturedUpdate);
        Assert.Contains("bartleby:in-progress", capturedUpdate!.Labels);
    }

    [Fact]
    public async Task UpdateStatusAsync_PreservesExistingLabels()
    {
        // Arrange
        var workItem = new WorkItem
        {
            ExternalId = "42",
            Status = WorkItemStatus.Ready,
            Labels = ["bug", "high-priority"]
        };

        GitHubIssueUpdate? capturedUpdate = null;
        _gitHubApiClientMock
            .Setup(c => c.UpdateIssueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                42,
                It.IsAny<GitHubIssueUpdate>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, GitHubIssueUpdate, CancellationToken>((o, r, n, u, ct) => capturedUpdate = u)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateStatusAsync(workItem);

        // Assert
        Assert.NotNull(capturedUpdate);
        Assert.Contains("bug", capturedUpdate!.Labels);
        Assert.Contains("high-priority", capturedUpdate.Labels);
        Assert.Contains("bartleby:ready", capturedUpdate.Labels);
    }

    [Fact]
    public async Task UpdateStatusAsync_RemovesOldStatusLabels_WhenTransitioningStatus()
    {
        // Arrange - work item has old "ready" label but status is now InProgress
        var workItem = new WorkItem
        {
            ExternalId = "42",
            Status = WorkItemStatus.InProgress,
            Labels = ["bug", "bartleby:ready", "high-priority"]
        };

        GitHubIssueUpdate? capturedUpdate = null;
        _gitHubApiClientMock
            .Setup(c => c.UpdateIssueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                42,
                It.IsAny<GitHubIssueUpdate>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, GitHubIssueUpdate, CancellationToken>((o, r, n, u, ct) => capturedUpdate = u)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateStatusAsync(workItem);

        // Assert - old status label removed, new one added, non-status labels preserved
        Assert.NotNull(capturedUpdate);
        Assert.Contains("bug", capturedUpdate!.Labels);
        Assert.Contains("high-priority", capturedUpdate.Labels);
        Assert.Contains("bartleby:in-progress", capturedUpdate.Labels);
        Assert.DoesNotContain("bartleby:ready", capturedUpdate.Labels);
    }

    #endregion

    #region AddCommentAsync Tests

    [Fact]
    public async Task AddCommentAsync_WhenExternalIdMissing_ThrowsArgumentException()
    {
        // Arrange
        var workItem = new WorkItem { ExternalId = null };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.AddCommentAsync(workItem, "test comment"));
    }

    [Fact]
    public async Task AddCommentAsync_WhenCommentEmpty_ThrowsArgumentException()
    {
        // Arrange
        var workItem = new WorkItem { ExternalId = "1" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.AddCommentAsync(workItem, ""));
    }

    [Fact]
    public async Task AddCommentAsync_WhenOwnerNotConfigured_ThrowsInvalidOperationException()
    {
        // Arrange
        _defaultSettings.GitHubOwner = null;
        var workItem = new WorkItem { ExternalId = "1" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.AddCommentAsync(workItem, "test comment"));
    }

    [Fact]
    public async Task AddCommentAsync_WithValidInput_CreatesComment()
    {
        // Arrange
        var workItem = new WorkItem { ExternalId = "42" };
        var comment = "This is a test comment";

        _gitHubApiClientMock
            .Setup(c => c.AddCommentAsync(
                _defaultSettings.GitHubOwner!,
                _defaultSettings.GitHubRepo!,
                42,
                comment,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.AddCommentAsync(workItem, comment);

        // Assert
        _gitHubApiClientMock.Verify(
            c => c.AddCommentAsync(
                _defaultSettings.GitHubOwner!,
                _defaultSettings.GitHubRepo!,
                42,
                comment,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region TestConnectionAsync Tests

    [Fact]
    public async Task TestConnectionAsync_WhenOwnerNotConfigured_ReturnsFalse()
    {
        // Arrange
        _defaultSettings.GitHubOwner = null;

        // Act
        var result = await _sut.TestConnectionAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenRepoNotConfigured_ReturnsFalse()
    {
        // Arrange
        _defaultSettings.GitHubRepo = null;

        // Act
        var result = await _sut.TestConnectionAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenConnectionSucceeds_ReturnsTrue()
    {
        // Arrange
        _gitHubApiClientMock
            .Setup(c => c.TestConnectionAsync(_defaultSettings.GitHubOwner!, _defaultSettings.GitHubRepo!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.TestConnectionAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenConnectionFails_ReturnsFalse()
    {
        // Arrange
        _gitHubApiClientMock
            .Setup(c => c.TestConnectionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.TestConnectionAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenApiThrows_ReturnsFalse()
    {
        // Arrange
        _gitHubApiClientMock
            .Setup(c => c.TestConnectionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection failed"));

        // Act
        var result = await _sut.TestConnectionAsync();

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullSettingsRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GitHubWorkSource(null!));
    }

    [Fact]
    public void Constructor_WithNullClientFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GitHubWorkSource(
            _settingsRepositoryMock.Object,
            (Func<string?, IGitHubApiClient>)null!));
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task SyncAsync_WithNullIssueTitle_MapsToEmptyString()
    {
        // Arrange
        var issue = new GitHubIssue(
            Number: 1,
            Title: null!,
            Body: "Body",
            HtmlUrl: "https://github.com/test-owner/test-repo/issues/1",
            Labels: [],
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            IsPullRequest: false);

        _gitHubApiClientMock
            .Setup(c => c.GetIssuesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubIssue> { issue });

        // Act
        var result = await _sut.SyncAsync();

        // Assert
        Assert.Equal(string.Empty, result.First().Title);
    }

    [Fact]
    public async Task SyncAsync_WithNullIssueBody_MapsToEmptyString()
    {
        // Arrange
        var issue = new GitHubIssue(
            Number: 1,
            Title: "Title",
            Body: null,
            HtmlUrl: "https://github.com/test-owner/test-repo/issues/1",
            Labels: [],
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            IsPullRequest: false);

        _gitHubApiClientMock
            .Setup(c => c.GetIssuesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubIssue> { issue });

        // Act
        var result = await _sut.SyncAsync();

        // Assert
        Assert.Equal(string.Empty, result.First().Description);
    }

    [Fact]
    public async Task SyncAsync_WithSpecialCharactersInTitle_PreservesThem()
    {
        // Arrange
        var specialTitle = "Fix bug with <script> & \"quotes\" in 'input'";
        var issue = CreateIssue(1, specialTitle, "Body");

        _gitHubApiClientMock
            .Setup(c => c.GetIssuesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubIssue> { issue });

        // Act
        var result = await _sut.SyncAsync();

        // Assert
        Assert.Equal(specialTitle, result.First().Title);
    }

    [Fact]
    public async Task SyncAsync_WithUnicodeInTitle_PreservesThem()
    {
        // Arrange
        var unicodeTitle = "Fix bug with emoji \ud83d\udc1b and unicode \u4e2d\u6587";
        var issue = CreateIssue(1, unicodeTitle, "Body");

        _gitHubApiClientMock
            .Setup(c => c.GetIssuesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubIssue> { issue });

        // Act
        var result = await _sut.SyncAsync();

        // Assert
        Assert.Equal(unicodeTitle, result.First().Title);
    }

    #endregion

    #region Helper Methods

    private static GitHubIssue CreateIssue(
        int number,
        string title,
        string body,
        string[]? labels = null,
        bool isPullRequest = false)
    {
        return new GitHubIssue(
            Number: number,
            Title: title,
            Body: body,
            HtmlUrl: $"https://github.com/test-owner/test-repo/issues/{number}",
            Labels: labels ?? [],
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            IsPullRequest: isPullRequest);
    }

    #endregion
}

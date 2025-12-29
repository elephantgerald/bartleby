using System.ClientModel;
using System.ClientModel.Primitives;
using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Bartleby.Infrastructure.AIProviders;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;

namespace Bartleby.Infrastructure.Tests.AIProviders;

public class AzureOpenAIProviderTests
{
    private readonly Mock<ISettingsRepository> _settingsRepositoryMock;
    private readonly Mock<ILogger<AzureOpenAIProvider>> _loggerMock;
    private readonly Mock<IChatClientWrapper> _chatClientMock;
    private readonly AppSettings _defaultSettings;
    private readonly AzureOpenAIProvider _sut;

    public AzureOpenAIProviderTests()
    {
        _settingsRepositoryMock = new Mock<ISettingsRepository>();
        _loggerMock = new Mock<ILogger<AzureOpenAIProvider>>();
        _chatClientMock = new Mock<IChatClientWrapper>();

        _defaultSettings = new AppSettings
        {
            AzureOpenAIEndpoint = "https://test.openai.azure.com",
            AzureOpenAIApiKey = "test-api-key",
            AzureOpenAIDeploymentName = "gpt-4"
        };

        _settingsRepositoryMock
            .Setup(r => r.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_defaultSettings);

        _sut = new AzureOpenAIProvider(
            _settingsRepositoryMock.Object,
            _loggerMock.Object,
            _ => _chatClientMock.Object);
    }

    #region Name Property

    [Fact]
    public void Name_ReturnsAzureOpenAI()
    {
        _sut.Name.Should().Be("Azure OpenAI");
    }

    #endregion

    #region ExecuteWorkAsync - Settings Validation

    [Fact]
    public async Task ExecuteWorkAsync_WhenEndpointNotConfigured_ReturnsFailedResult()
    {
        // Arrange
        _defaultSettings.AzureOpenAIEndpoint = null;
        var workItem = CreateWorkItem();

        // Act
        var result = await _sut.ExecuteWorkAsync(workItem, "/work");

        // Assert
        result.Success.Should().BeFalse();
        result.Outcome.Should().Be(WorkExecutionOutcome.Failed);
        result.ErrorMessage.Should().Contain("endpoint");
    }

    [Fact]
    public async Task ExecuteWorkAsync_WhenApiKeyNotConfigured_ReturnsFailedResult()
    {
        // Arrange
        _defaultSettings.AzureOpenAIApiKey = null;
        var workItem = CreateWorkItem();

        // Act
        var result = await _sut.ExecuteWorkAsync(workItem, "/work");

        // Assert
        result.Success.Should().BeFalse();
        result.Outcome.Should().Be(WorkExecutionOutcome.Failed);
        result.ErrorMessage.Should().Contain("API key");
    }

    [Fact]
    public async Task ExecuteWorkAsync_WhenDeploymentNameNotConfigured_ReturnsFailedResult()
    {
        // Arrange
        _defaultSettings.AzureOpenAIDeploymentName = null;
        var workItem = CreateWorkItem();

        // Act
        var result = await _sut.ExecuteWorkAsync(workItem, "/work");

        // Assert
        result.Success.Should().BeFalse();
        result.Outcome.Should().Be(WorkExecutionOutcome.Failed);
        result.ErrorMessage.Should().Contain("deployment name");
    }

    [Fact]
    public async Task ExecuteWorkAsync_WhenWorkItemIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ExecuteWorkAsync(null!, "/work"));
    }

    [Fact]
    public async Task ExecuteWorkAsync_WhenWorkingDirectoryIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var workItem = CreateWorkItem();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ExecuteWorkAsync(workItem, ""));
    }

    #endregion

    #region ExecuteWorkAsync - API Responses

    [Fact]
    public async Task ExecuteWorkAsync_WhenApiReturnsCompletedJson_ReturnsSuccessResult()
    {
        // Arrange
        var workItem = CreateWorkItem();
        var completion = CreateChatCompletion("""
            {
                "outcome": "completed",
                "summary": "Implemented the feature",
                "modified_files": ["src/file.cs"],
                "questions": []
            }
            """, inputTokens: 100, outputTokens: 50);

        _chatClientMock
            .Setup(c => c.CompleteChatAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(completion);

        // Act
        var result = await _sut.ExecuteWorkAsync(workItem, "/work");

        // Assert
        result.Success.Should().BeTrue();
        result.Outcome.Should().Be(WorkExecutionOutcome.Completed);
        result.Summary.Should().Be("Implemented the feature");
        result.ModifiedFiles.Should().ContainSingle().Which.Should().Be("src/file.cs");
        result.TokensUsed.Should().Be(150);
    }

    [Fact]
    public async Task ExecuteWorkAsync_WhenApiReturnsBlockedJson_ReturnsBlockedResult()
    {
        // Arrange
        var workItem = CreateWorkItem();
        var completion = CreateChatCompletion("""
            {
                "outcome": "blocked",
                "summary": "Need more information",
                "modified_files": [],
                "questions": ["What database should be used?", "What is the authentication method?"]
            }
            """);

        _chatClientMock
            .Setup(c => c.CompleteChatAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(completion);

        // Act
        var result = await _sut.ExecuteWorkAsync(workItem, "/work");

        // Assert
        result.Success.Should().BeFalse();
        result.Outcome.Should().Be(WorkExecutionOutcome.Blocked);
        result.Questions.Should().HaveCount(2);
        result.Questions.Should().Contain("What database should be used?");
    }

    [Fact]
    public async Task ExecuteWorkAsync_WhenApiReturnsNeedsContextJson_ReturnsNeedsMoreContextResult()
    {
        // Arrange
        var workItem = CreateWorkItem();
        var completion = CreateChatCompletion("""
            {
                "outcome": "needs_context",
                "summary": "Cannot find the relevant files",
                "modified_files": [],
                "questions": []
            }
            """);

        _chatClientMock
            .Setup(c => c.CompleteChatAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(completion);

        // Act
        var result = await _sut.ExecuteWorkAsync(workItem, "/work");

        // Assert
        result.Success.Should().BeFalse();
        result.Outcome.Should().Be(WorkExecutionOutcome.NeedsMoreContext);
    }

    [Fact]
    public async Task ExecuteWorkAsync_WhenApiReturnsPlainText_TreatsAsCompleted()
    {
        // Arrange
        var workItem = CreateWorkItem();
        var completion = CreateChatCompletion("I completed the task successfully.");

        _chatClientMock
            .Setup(c => c.CompleteChatAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(completion);

        // Act
        var result = await _sut.ExecuteWorkAsync(workItem, "/work");

        // Assert
        result.Success.Should().BeTrue();
        result.Outcome.Should().Be(WorkExecutionOutcome.Completed);
        result.Summary.Should().Be("I completed the task successfully.");
    }

    [Fact]
    public async Task ExecuteWorkAsync_WhenApiReturnsInvalidJson_FallsBackToRawContent()
    {
        // Arrange
        var workItem = CreateWorkItem();
        var completion = CreateChatCompletion("Here is a partial json { broken...");

        _chatClientMock
            .Setup(c => c.CompleteChatAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(completion);

        // Act
        var result = await _sut.ExecuteWorkAsync(workItem, "/work");

        // Assert
        result.Success.Should().BeTrue();
        result.Outcome.Should().Be(WorkExecutionOutcome.Completed);
        result.Summary.Should().Contain("partial json");
    }

    #endregion

    #region ExecuteWorkAsync - Error Handling

    [Fact]
    public async Task ExecuteWorkAsync_WhenAuthenticationFails_ReturnsAuthErrorResult()
    {
        // Arrange
        var workItem = CreateWorkItem();

        _chatClientMock
            .Setup(c => c.CompleteChatAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateClientResultException(401));

        // Act
        var result = await _sut.ExecuteWorkAsync(workItem, "/work");

        // Assert
        result.Success.Should().BeFalse();
        result.Outcome.Should().Be(WorkExecutionOutcome.Failed);
        result.ErrorMessage.Should().Contain("authentication");
    }

    [Fact]
    public async Task ExecuteWorkAsync_WhenForbidden_ReturnsAuthErrorResult()
    {
        // Arrange
        var workItem = CreateWorkItem();

        _chatClientMock
            .Setup(c => c.CompleteChatAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateClientResultException(403));

        // Act
        var result = await _sut.ExecuteWorkAsync(workItem, "/work");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("authentication");
    }

    [Fact]
    public async Task ExecuteWorkAsync_WhenRateLimited_ReturnsRateLimitErrorResult()
    {
        // Arrange
        var workItem = CreateWorkItem();

        _chatClientMock
            .Setup(c => c.CompleteChatAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateClientResultException(429));

        // Act
        var result = await _sut.ExecuteWorkAsync(workItem, "/work");

        // Assert
        result.Success.Should().BeFalse();
        result.Outcome.Should().Be(WorkExecutionOutcome.Failed);
        result.ErrorMessage.Should().Contain("Rate limit");
    }

    [Fact]
    public async Task ExecuteWorkAsync_WhenGenericExceptionThrown_ReturnsFailedResult()
    {
        // Arrange
        var workItem = CreateWorkItem();

        _chatClientMock
            .Setup(c => c.CompleteChatAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Something went wrong"));

        // Act
        var result = await _sut.ExecuteWorkAsync(workItem, "/work");

        // Assert
        result.Success.Should().BeFalse();
        result.Outcome.Should().Be(WorkExecutionOutcome.Failed);
        result.ErrorMessage.Should().Contain("Something went wrong");
    }

    #endregion

    #region TestConnectionAsync

    [Fact]
    public async Task TestConnectionAsync_WhenApiResponds_ReturnsTrue()
    {
        // Arrange
        var completion = CreateChatCompletion("OK");

        _chatClientMock
            .Setup(c => c.CompleteChatAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(completion);

        // Act
        var result = await _sut.TestConnectionAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_WhenApiThrows_ReturnsFalse()
    {
        // Arrange
        _chatClientMock
            .Setup(c => c.CompleteChatAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        // Act
        var result = await _sut.TestConnectionAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TestConnectionAsync_WhenEndpointNotConfigured_ReturnsFalse()
    {
        // Arrange
        _defaultSettings.AzureOpenAIEndpoint = null;

        // Act
        var result = await _sut.TestConnectionAsync();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Token Counting

    [Fact]
    public async Task ExecuteWorkAsync_TracksTokenUsageCorrectly()
    {
        // Arrange
        var workItem = CreateWorkItem();
        var completion = CreateChatCompletion(
            """{"outcome": "completed", "summary": "Done", "modified_files": [], "questions": []}""",
            inputTokens: 500,
            outputTokens: 200);

        _chatClientMock
            .Setup(c => c.CompleteChatAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(completion);

        // Act
        var result = await _sut.ExecuteWorkAsync(workItem, "/work");

        // Assert
        result.TokensUsed.Should().Be(700);
    }

    #endregion

    #region Helper Methods

    private static WorkItem CreateWorkItem(
        string title = "Test Work Item",
        string description = "Test description")
    {
        return new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Status = WorkItemStatus.Ready,
            Labels = ["test", "feature"],
            ExternalUrl = "https://github.com/test/repo/issues/1"
        };
    }

    private static ChatCompletion CreateChatCompletion(
        string content,
        int inputTokens = 100,
        int outputTokens = 50)
    {
        // Create a mock ChatCompletion using ModelReaderWriter from BinaryData
        // This is the recommended way to create test instances in the new Azure SDK
        var json = $$"""
            {
                "id": "chatcmpl-test",
                "object": "chat.completion",
                "created": 1234567890,
                "model": "gpt-4",
                "choices": [
                    {
                        "index": 0,
                        "message": {
                            "role": "assistant",
                            "content": {{System.Text.Json.JsonSerializer.Serialize(content)}}
                        },
                        "finish_reason": "stop"
                    }
                ],
                "usage": {
                    "prompt_tokens": {{inputTokens}},
                    "completion_tokens": {{outputTokens}},
                    "total_tokens": {{inputTokens + outputTokens}}
                }
            }
            """;

        var binaryData = BinaryData.FromString(json);
        return ModelReaderWriter.Read<ChatCompletion>(binaryData)!;
    }

    private static ClientResultException CreateClientResultException(int statusCode)
    {
        // Create a ClientResultException with a specific status code
        var response = new MockPipelineResponse(statusCode);
        return new ClientResultException(response);
    }

    /// <summary>
    /// Mock implementation of PipelineResponse for testing.
    /// </summary>
    private sealed class MockPipelineResponse : PipelineResponse
    {
        private readonly int _status;

        public MockPipelineResponse(int status)
        {
            _status = status;
        }

        public override int Status => _status;

        public override string ReasonPhrase => _status switch
        {
            401 => "Unauthorized",
            403 => "Forbidden",
            429 => "Too Many Requests",
            500 => "Internal Server Error",
            _ => "Error"
        };

        public override Stream? ContentStream
        {
            get => null;
            set { }
        }

        public override BinaryData Content => BinaryData.Empty;

        protected override PipelineResponseHeaders HeadersCore =>
            throw new NotImplementedException();

        public override void Dispose() { }

        public override BinaryData BufferContent(CancellationToken cancellationToken = default)
        {
            return BinaryData.Empty;
        }

        public override ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default)
        {
            return new ValueTask<BinaryData>(BinaryData.Empty);
        }
    }

    #endregion
}

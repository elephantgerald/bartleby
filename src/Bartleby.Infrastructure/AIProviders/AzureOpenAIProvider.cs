using System.ClientModel;
using Azure.AI.OpenAI;
using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Polly;
using Polly.Retry;

namespace Bartleby.Infrastructure.AIProviders;

/// <summary>
/// Azure OpenAI implementation of IAIProvider.
/// </summary>
public class AzureOpenAIProvider : IAIProvider
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<AzureOpenAIProvider> _logger;
    private readonly Func<AppSettings, IChatClientWrapper>? _chatClientFactory;
    private readonly ResiliencePipeline<ChatCompletion> _resiliencePipeline;

    public string Name => "Azure OpenAI";

    /// <summary>
    /// Creates a new AzureOpenAIProvider for production use.
    /// </summary>
    public AzureOpenAIProvider(
        ISettingsRepository settingsRepository,
        ILogger<AzureOpenAIProvider> logger)
        : this(settingsRepository, logger, null)
    {
    }

    /// <summary>
    /// Creates a new AzureOpenAIProvider with an optional factory for testing.
    /// </summary>
    internal AzureOpenAIProvider(
        ISettingsRepository settingsRepository,
        ILogger<AzureOpenAIProvider> logger,
        Func<AppSettings, IChatClientWrapper>? chatClientFactory)
    {
        _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _chatClientFactory = chatClientFactory;
        _resiliencePipeline = CreateResiliencePipeline();
    }

    public async Task<WorkExecutionResult> ExecuteWorkAsync(
        WorkItem workItem,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        try
        {
            var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);
            ValidateSettings(settings);

            var chatClient = CreateChatClientWrapper(settings);
            var messages = BuildPromptMessages(workItem, workingDirectory);

            _logger.LogInformation(
                "Executing work on item {WorkItemId}: {WorkItemTitle}",
                workItem.Id,
                workItem.Title);

            var completion = await _resiliencePipeline.ExecuteAsync(
                async ct => await chatClient.CompleteChatAsync(messages, ct),
                cancellationToken);

            return ParseCompletionResult(completion, workItem);
        }
        catch (ClientResultException ex) when (IsAuthenticationError(ex))
        {
            _logger.LogError(ex, "Authentication failed for Azure OpenAI");
            return new WorkExecutionResult
            {
                Success = false,
                Outcome = WorkExecutionOutcome.Failed,
                ErrorMessage = "Azure OpenAI authentication failed. Please check your API key and endpoint.",
                TokensUsed = 0
            };
        }
        catch (ClientResultException ex) when (IsRateLimitError(ex))
        {
            _logger.LogWarning(ex, "Rate limit exceeded for Azure OpenAI after retries");
            return new WorkExecutionResult
            {
                Success = false,
                Outcome = WorkExecutionOutcome.Failed,
                ErrorMessage = "Rate limit exceeded. Please try again later.",
                TokensUsed = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing work on item {WorkItemId}", workItem.Id);
            return new WorkExecutionResult
            {
                Success = false,
                Outcome = WorkExecutionOutcome.Failed,
                ErrorMessage = $"Error executing work: {ex.Message}",
                TokensUsed = 0
            };
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);
            ValidateSettings(settings);

            var chatClient = CreateChatClientWrapper(settings);

            // Send a simple test message
            var messages = new List<ChatMessage>
            {
                new UserChatMessage("Say 'OK' if you can hear me.")
            };

            await chatClient.CompleteChatAsync(messages, cancellationToken);

            _logger.LogInformation("Azure OpenAI connection test successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI connection test failed");
            return false;
        }
    }

    private IChatClientWrapper CreateChatClientWrapper(AppSettings settings)
    {
        if (_chatClientFactory != null)
        {
            return _chatClientFactory(settings);
        }

        var azureClient = new AzureOpenAIClient(
            new Uri(settings.AzureOpenAIEndpoint!),
            new ApiKeyCredential(settings.AzureOpenAIApiKey!));

        var chatClient = azureClient.GetChatClient(settings.AzureOpenAIDeploymentName!);
        return new ChatClientWrapper(chatClient);
    }

    private static void ValidateSettings(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.AzureOpenAIEndpoint))
        {
            throw new InvalidOperationException("Azure OpenAI endpoint is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.AzureOpenAIApiKey))
        {
            throw new InvalidOperationException("Azure OpenAI API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.AzureOpenAIDeploymentName))
        {
            throw new InvalidOperationException("Azure OpenAI deployment name is not configured.");
        }
    }

    private static List<ChatMessage> BuildPromptMessages(WorkItem workItem, string workingDirectory)
    {
        var systemPrompt = $$"""
            You are an autonomous software development assistant working on a codebase.

            Working directory: {{workingDirectory}}

            Your task is to complete the work item described below. Analyze the requirements,
            implement the necessary changes, and provide a summary of what was done.

            If you cannot complete the work due to missing information or dependencies,
            clearly state what questions need to be answered before proceeding.

            Respond in the following JSON format:
            {
                "outcome": "completed" | "blocked" | "needs_context",
                "summary": "Brief description of what was accomplished or why blocked",
                "modified_files": ["list", "of", "modified", "files"],
                "questions": ["list of questions if blocked"]
            }
            """;

        var userPrompt = $"""
            Work Item: {workItem.Title}

            Description:
            {workItem.Description}

            Labels: {string.Join(", ", workItem.Labels)}

            External Reference: {workItem.ExternalUrl ?? "N/A"}
            """;

        return
        [
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        ];
    }

    private WorkExecutionResult ParseCompletionResult(ChatCompletion completion, WorkItem workItem)
    {
        var content = completion.Content.FirstOrDefault()?.Text ?? string.Empty;
        var tokensUsed = CalculateTokensUsed(completion);

        _logger.LogDebug(
            "Received response for work item {WorkItemId}: {TokensUsed} tokens used",
            workItem.Id,
            tokensUsed);

        // Try to parse as JSON response
        try
        {
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = content[jsonStart..(jsonEnd + 1)];
                var response = System.Text.Json.JsonSerializer.Deserialize<AiResponsePayload>(
                    jsonContent,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (response != null)
                {
                    var outcome = response.Outcome?.ToLowerInvariant() switch
                    {
                        "completed" => WorkExecutionOutcome.Completed,
                        "blocked" => WorkExecutionOutcome.Blocked,
                        "needs_context" => WorkExecutionOutcome.NeedsMoreContext,
                        _ => WorkExecutionOutcome.Completed
                    };

                    return new WorkExecutionResult
                    {
                        Success = outcome == WorkExecutionOutcome.Completed,
                        Outcome = outcome,
                        Summary = response.Summary ?? content,
                        ModifiedFiles = response.ModifiedFiles ?? [],
                        Questions = response.Questions ?? [],
                        TokensUsed = tokensUsed
                    };
                }
            }
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response as JSON, using raw content");
        }

        // Fallback: treat raw response as completed
        return new WorkExecutionResult
        {
            Success = true,
            Outcome = WorkExecutionOutcome.Completed,
            Summary = content,
            ModifiedFiles = [],
            Questions = [],
            TokensUsed = tokensUsed
        };
    }

    private static int CalculateTokensUsed(ChatCompletion completion)
    {
        var usage = completion.Usage;
        if (usage != null)
        {
            return usage.InputTokenCount + usage.OutputTokenCount;
        }
        return 0;
    }

    private ResiliencePipeline<ChatCompletion> CreateResiliencePipeline()
    {
        return new ResiliencePipelineBuilder<ChatCompletion>()
            .AddRetry(new RetryStrategyOptions<ChatCompletion>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<ChatCompletion>()
                    .Handle<ClientResultException>(ex => IsRetryableError(ex))
            })
            .Build();
    }

    private static bool IsRetryableError(ClientResultException ex)
    {
        // Retry on rate limiting (429) and server errors (5xx)
        return IsRateLimitError(ex) || IsServerError(ex);
    }

    private static bool IsRateLimitError(ClientResultException ex)
    {
        return ex.Status == 429;
    }

    private static bool IsAuthenticationError(ClientResultException ex)
    {
        return ex.Status == 401 || ex.Status == 403;
    }

    private static bool IsServerError(ClientResultException ex)
    {
        return ex.Status >= 500 && ex.Status < 600;
    }

    /// <summary>
    /// Internal DTO for parsing AI response JSON.
    /// </summary>
    private sealed class AiResponsePayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("outcome")]
        public string? Outcome { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("modified_files")]
        public List<string>? ModifiedFiles { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("questions")]
        public List<string>? Questions { get; set; }
    }
}

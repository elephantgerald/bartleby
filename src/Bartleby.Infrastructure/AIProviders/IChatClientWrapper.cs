using OpenAI.Chat;

namespace Bartleby.Infrastructure.AIProviders;

/// <summary>
/// Wrapper interface for ChatClient to enable testing.
/// </summary>
public interface IChatClientWrapper
{
    /// <summary>
    /// Sends a chat completion request with optional options.
    /// </summary>
    Task<ChatCompletion> CompleteChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation that wraps the real ChatClient.
/// </summary>
internal sealed class ChatClientWrapper : IChatClientWrapper
{
    private readonly ChatClient _chatClient;

    public ChatClientWrapper(ChatClient chatClient)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    }

    public async Task<ChatCompletion> CompleteChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
        return response.Value;
    }
}

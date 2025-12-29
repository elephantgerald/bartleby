using OpenAI.Chat;

namespace Bartleby.Infrastructure.AIProviders;

/// <summary>
/// Wrapper interface for ChatClient to enable testing.
/// </summary>
public interface IChatClientWrapper
{
    /// <summary>
    /// Sends a chat completion request.
    /// </summary>
    Task<ChatCompletion> CompleteChatAsync(
        IEnumerable<ChatMessage> messages,
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
        CancellationToken cancellationToken = default)
    {
        var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        return response.Value;
    }
}

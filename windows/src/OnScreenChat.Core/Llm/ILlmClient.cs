using OnScreenChat.Core.Data;

namespace OnScreenChat.Core.Llm;

/// <summary>
/// The seam between the UI and whatever produces replies. The chat layer is
/// written entirely against this interface, so a new provider drops in without
/// touching the UI.
/// </summary>
public interface ILlmClient
{
    /// <summary>Streams the assistant reply token-by-token (or chunk-by-chunk).</summary>
    IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        string systemPrompt,
        CancellationToken cancellationToken = default);
}

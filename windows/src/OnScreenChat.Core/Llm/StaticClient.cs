using OnScreenChat.Core.Data;

namespace OnScreenChat.Core.Llm;

/// <summary>
/// Yields one fixed message and stops. Used for conditions the user must see in
/// the transcript rather than as a silent failure — chiefly a missing API key,
/// which on Windows has no local-model fallback to soften it.
/// </summary>
public sealed class StaticClient(string message) : ILlmClient
{
    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        string systemPrompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        yield return message;
    }
}

using System.Runtime.CompilerServices;
using OnScreenChat.Core.Data;

namespace OnScreenChat.Core.Llm;

/// <summary>
/// Fakes a streamed reply so the transcript, autoscroll, and busy state are
/// testable with no network and no API key. With local models dropped, this is
/// also the only way to exercise the UI before a Gemini key is configured.
/// </summary>
public sealed class EchoClient : ILlmClient
{
    private readonly TimeSpan _delay;

    public EchoClient(TimeSpan? delayPerWord = null) =>
        _delay = delayPerWord ?? TimeSpan.FromMilliseconds(35);

    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        string systemPrompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var lastUser = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? "";
        var skillNote = string.IsNullOrEmpty(systemPrompt) ? "Plain chat" : "skill active";
        var reply = $"[mock · {skillNote}] I received: \"{lastUser}\". " +
                    "No model was called — this is the streaming UI only.";

        foreach (var word in reply.Split(' '))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return word + " ";
            if (_delay > TimeSpan.Zero)
            {
                await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

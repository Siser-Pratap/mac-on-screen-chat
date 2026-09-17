using System.Collections.ObjectModel;
using OnScreenChat.Core.Data;
using OnScreenChat.Core.Llm;
using OnScreenChat.Core.Prompts;
using OnScreenChat.Core.Text;

namespace OnScreenChat.Core.Chat;

/// <summary>
/// The stateful half of the Mac app's <c>ChatViewModel</c>: owns the transcript,
/// runs a reply, and persists the result. Kept free of UI-framework types so the
/// ordering rules that are easy to get wrong — what reaches the model, what a
/// stopped reply is allowed to touch — can be tested without a Windows machine.
/// </summary>
public sealed class ChatSession
{
    private readonly AppDatabase _database;
    private readonly RuleStore _rules;

    private CancellationTokenSource? _streaming;

    /// <summary>
    /// Bumped by every send and every stop, so a reply that finishes late can
    /// tell whether it's still the current one before touching shared state.
    /// </summary>
    private int _generation;

    public ChatSession(AppDatabase database, RuleStore rules)
    {
        _database = database;
        _rules = rules;
        Messages = new ObservableCollection<ChatMessage>(database.LoadMessages());
    }

    public ObservableCollection<ChatMessage> Messages { get; }

    public bool IsStreaming { get; private set; }

    /// <summary>Raised when <see cref="IsStreaming"/> changes.</summary>
    public event Action? StreamingChanged;

    /// <summary>
    /// Handles one send. Returns false when nothing was dispatched to the model
    /// — an empty input, a reply already in flight, or a <c>/command {…}</c>
    /// that only saved rules.
    /// </summary>
    public async Task<bool> SendAsync(string input, string systemPrompt, ILlmClient client)
    {
        var trimmed = input.Trim();
        if (trimmed.Length == 0 || IsStreaming) return false;

        // `/command {...}` captures a STANDING rule: saved, applied to every
        // future reply, and kept out of the transcript entirely.
        var (afterCommands, commands) = PromptBuilder.ExtractCommands(trimmed);
        var saved = commands.Select(_rules.Add).OfType<string>().ToList();

        // `WW:{...}` steers only THIS reply and is likewise kept out.
        var (afterDirective, directive) = PromptBuilder.ExtractDirective(afterCommands);
        var messageText = afterDirective.Trim();

        // Commands with nothing else to ask: acknowledge, don't call the model.
        if (messageText.Length == 0 && commands.Count > 0)
        {
            AppendNote(SavedRuleNote(saved, commands.Count));
            return false;
        }

        // A bare `WW:{...}` has no message to steer — send it as written rather
        // than firing an empty request.
        var outgoing = messageText.Length == 0 ? trimmed : messageText;

        Append(ChatRole.User, outgoing);

        if (saved.Count > 0)
        {
            AppendNote(SavedRuleNote(saved, commands.Count));
        }

        var assistant = new ChatMessage(ChatRole.Assistant);
        Messages.Add(assistant);
        var order = Messages.Count - 1;

        var effectiveSystem = PromptBuilder.SystemPrompt(systemPrompt, _rules.PromptBlock, directive);

        SetStreaming(true);
        _generation++;
        var generation = _generation;

        _streaming = new CancellationTokenSource();
        var token = _streaming.Token;
        var snapshot = Messages.ToList();

        // Guarantees no `**` reaches the transcript, whatever the model does.
        var stripper = new AsteriskStripper();

        try
        {
            // Deliberately NOT ConfigureAwait(false): the continuation must come
            // back to the caller's context. On Windows that is the UI thread, and
            // mutating the bound collection from a pool thread would crash WinUI.
            await foreach (var chunk in client.StreamAsync(snapshot, effectiveSystem, token)
                               .WithCancellation(token))
            {
                var clean = stripper.Push(chunk);
                if (clean.Length > 0) assistant.Text += clean;
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped by the user: keep whatever already arrived.
        }

        Finish(assistant, order, stripper.Flush(), generation);
        return true;
    }

    /// <summary>Cancels a reply in flight, keeping whatever text already arrived.</summary>
    public void Stop()
    {
        if (!IsStreaming) return;

        _generation++; // the cancelled reply no longer owns the shared state
        _streaming?.Cancel();
        _streaming = null;
        SetStreaming(false);
    }

    public void NewChat()
    {
        _generation++;
        _streaming?.Cancel();
        _streaming = null;
        SetStreaming(false);
        Messages.Clear();
        _database.ClearMessages();
    }

    /// <summary>
    /// Ends a reply: tidies the text, drops a bubble that never received
    /// anything (a stop before the first token), and persists the result.
    /// A stopped reply still lands here to keep what it had, but by then it's a
    /// stale generation and must not clear the flags of whatever came next.
    /// </summary>
    private void Finish(ChatMessage assistant, int order, string tail, int generation)
    {
        try
        {
            if (!Messages.Contains(assistant)) return;

            var text = ResponseStyle.Normalized(assistant.Text + tail);
            if (text.Length == 0)
            {
                Messages.Remove(assistant);
                return;
            }

            assistant.Text = text;
            _database.AppendMessage(ChatRole.Assistant, text, order);
        }
        finally
        {
            if (generation == _generation)
            {
                _streaming = null;
                SetStreaming(false);
            }
        }
    }

    private void Append(ChatRole role, string text)
    {
        Messages.Add(new ChatMessage(role, text));
        _database.AppendMessage(role, text, Messages.Count - 1);
    }

    private void AppendNote(string text) => Append(ChatRole.Note, text);

    private void SetStreaming(bool value)
    {
        if (IsStreaming == value) return;
        IsStreaming = value;
        StreamingChanged?.Invoke();
    }

    /// <summary>
    /// Wording for the transcript after <c>/command {…}</c> — including the case
    /// where a rule was a duplicate and nothing new was stored.
    /// </summary>
    internal static string SavedRuleNote(IReadOnlyList<string> saved, int attempted)
    {
        if (saved.Count == 0)
        {
            return attempted == 1
                ? "Already saved — that rule is in effect."
                : "Already saved — those rules are in effect.";
        }

        var list = string.Join("\n", saved.Select(rule => $"- {rule}"));
        var lead = saved.Count == 1
            ? "Rule saved. It applies to every reply from now on:"
            : "Rules saved. They apply to every reply from now on:";
        return $"{lead}\n{list}";
    }
}

using OnScreenChat.Core.Chat;
using OnScreenChat.Core.Data;
using OnScreenChat.Core.Llm;

namespace OnScreenChat.Core.Tests;

public sealed class ChatSessionTests : IDisposable
{
    private readonly string _directory;
    private readonly AppDatabase _database;
    private readonly RuleStore _rules;
    private readonly ChatSession _session;

    public ChatSessionTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"onscreenchat-chat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _database = new AppDatabase(Path.Combine(_directory, "app.sqlite"));
        _rules = new RuleStore(_database);
        _session = new ChatSession(_database, _rules);
    }

    public void Dispose()
    {
        _database.Dispose();
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }

    private static ILlmClient Replying(string text) => new StaticClient(text);

    // MARK: - The basic turn

    [Fact]
    public async Task A_send_produces_a_user_turn_and_a_reply()
    {
        await _session.SendAsync("hello", "", Replying("hi there"));

        Assert.Equal([ChatRole.User, ChatRole.Assistant], _session.Messages.Select(m => m.Role));
        Assert.Equal("hi there", _session.Messages[1].Text);
    }

    [Fact]
    public async Task Empty_input_does_nothing()
    {
        Assert.False(await _session.SendAsync("   ", "", Replying("x")));
        Assert.Empty(_session.Messages);
    }

    [Fact]
    public async Task The_transcript_survives_a_restart()
    {
        await _session.SendAsync("hello", "", Replying("hi there"));

        var reopened = new ChatSession(_database, _rules);
        Assert.Equal(["hello", "hi there"], reopened.Messages.Select(m => m.Text));
    }

    [Fact]
    public async Task A_reply_that_arrives_empty_leaves_no_stray_bubble()
    {
        await _session.SendAsync("hello", "", Replying("   "));

        Assert.Equal([ChatRole.User], _session.Messages.Select(m => m.Role));
    }

    [Fact]
    public async Task Emphasis_never_reaches_the_transcript()
    {
        await _session.SendAsync("hello", "", Replying("that is **very** bold"));

        Assert.Equal("that is very bold", _session.Messages[1].Text);
    }

    [Fact]
    public async Task The_reply_is_normalized_before_it_is_stored()
    {
        await _session.SendAsync("hello", "", Replying("One.\n\n\n\nTwo.   "));

        Assert.Equal("One.\n\nTwo.", _session.Messages[1].Text);
        Assert.Equal("One.\n\nTwo.", new ChatSession(_database, _rules).Messages[1].Text);
    }

    [Fact]
    public async Task Streaming_is_false_again_once_the_reply_lands()
    {
        await _session.SendAsync("hello", "", Replying("hi"));
        Assert.False(_session.IsStreaming);
    }

    // MARK: - Markers

    [Fact]
    public async Task A_rules_only_message_saves_the_rule_and_calls_no_model()
    {
        var dispatched = await _session.SendAsync("/command {be brief}", "", Replying("SHOULD NOT APPEAR"));

        Assert.False(dispatched);
        Assert.Equal([ChatRole.Note], _session.Messages.Select(m => m.Role));
        Assert.Contains("Rule saved", _session.Messages[0].Text);
        Assert.Equal(["be brief"], _rules.Rules.Select(rule => rule.Text));
    }

    [Fact]
    public async Task A_duplicate_rule_says_so_rather_than_claiming_a_save()
    {
        await _session.SendAsync("/command {be brief}", "", Replying("x"));
        await _session.SendAsync("/command {BE BRIEF}", "", Replying("x"));

        Assert.Contains("Already saved", _session.Messages[1].Text);
        Assert.Single(_rules.Rules);
    }

    [Fact]
    public async Task A_rule_alongside_a_question_saves_it_and_still_asks()
    {
        var dispatched = await _session.SendAsync("summarize this /command {no emojis}", "", Replying("ok"));

        Assert.True(dispatched);
        Assert.Equal(
            [ChatRole.User, ChatRole.Note, ChatRole.Assistant],
            _session.Messages.Select(m => m.Role));
        Assert.Equal("summarize this", _session.Messages[0].Text);
    }

    [Fact]
    public async Task Markers_never_appear_in_the_transcript()
    {
        await _session.SendAsync("rewrite this WW:{one line}", "", Replying("ok"));

        Assert.Equal("rewrite this", _session.Messages[0].Text);
        Assert.DoesNotContain("WW:", _session.Messages[0].Text);
    }

    [Fact]
    public async Task A_bare_directive_is_sent_as_written_rather_than_as_an_empty_request()
    {
        await _session.SendAsync("WW:{one line}", "", Replying("ok"));

        Assert.Equal("WW:{one line}", _session.Messages[0].Text);
    }

    [Fact]
    public async Task Standing_rules_ride_along_with_the_request()
    {
        _rules.Add("always answer in under five lines");
        var spy = new PromptSpy();

        await _session.SendAsync("hello", "SKILL", spy);

        Assert.Contains("SKILL", spy.SystemPrompt);
        Assert.Contains("always answer in under five lines", spy.SystemPrompt);
    }

    [Fact]
    public async Task A_one_shot_directive_outranks_the_rules()
    {
        _rules.Add("be brief");
        var spy = new PromptSpy();

        await _session.SendAsync("hello WW:{be verbose}", "SKILL", spy);

        Assert.True(
            spy.SystemPrompt.IndexOf("be brief", StringComparison.Ordinal) <
            spy.SystemPrompt.IndexOf("be verbose", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Notes_are_filtered_out_before_the_request_goes_out()
    {
        // The session hands the client the whole transcript, notes included —
        // it is the request builder that drops them, exactly as on macOS. This
        // asserts the end of that chain, which is what actually matters.
        await _session.SendAsync("/command {be brief}", "", Replying("x")); // leaves a note
        var spy = new PromptSpy();

        await _session.SendAsync("hello", "", spy);

        Assert.Contains(spy.Messages, message => message.Role == ChatRole.Note);
        var body = GeminiProtocol.BuildRequestBody(spy.Messages, "");
        Assert.DoesNotContain("Rule saved", body);
    }

    // MARK: - Stop and new chat

    [Fact]
    public async Task Stopping_keeps_what_already_arrived()
    {
        var client = new BlockingClient();
        var send = _session.SendAsync("hello", "", client);

        await client.EmittedFirstChunk;
        _session.Stop();
        await send;

        Assert.Equal("partial", _session.Messages[1].Text);
        Assert.False(_session.IsStreaming);
    }

    [Fact]
    public async Task A_stopped_reply_does_not_clear_the_flags_of_the_next_one()
    {
        // The generation counter exists for exactly this: a late-finishing reply
        // must not report "idle" over a send that started after it.
        var slow = new BlockingClient();
        var first = _session.SendAsync("first", "", slow);

        await slow.EmittedFirstChunk;
        _session.Stop();

        var second = _session.SendAsync("second", "", new BlockingClient());
        slow.Release();
        await first;

        Assert.True(_session.IsStreaming); // the second reply still owns the state
        _session.Stop();
        await second;
    }

    [Fact]
    public async Task A_second_send_is_refused_while_one_is_streaming()
    {
        var client = new BlockingClient();
        var first = _session.SendAsync("hello", "", client);
        await client.EmittedFirstChunk;

        Assert.False(await _session.SendAsync("again", "", Replying("x")));

        _session.Stop();
        await first;
    }

    [Fact]
    public async Task New_chat_clears_the_transcript_and_the_stored_history()
    {
        await _session.SendAsync("hello", "", Replying("hi"));
        _session.NewChat();

        Assert.Empty(_session.Messages);
        Assert.Empty(new ChatSession(_database, _rules).Messages);
    }

    [Fact]
    public async Task New_chat_leaves_the_standing_rules_alone()
    {
        // Rules outliving "New chat" is the whole point of them.
        _rules.Add("be brief");
        await _session.SendAsync("hello", "", Replying("hi"));

        _session.NewChat();

        Assert.Equal(["be brief"], _rules.Rules.Select(rule => rule.Text));
    }

    // MARK: - Test doubles

    private sealed class PromptSpy : ILlmClient
    {
        public string SystemPrompt { get; private set; } = "";
        public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];

        public async IAsyncEnumerable<string> StreamAsync(
            IReadOnlyList<ChatMessage> messages,
            string systemPrompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            SystemPrompt = systemPrompt;
            Messages = messages;
            await Task.CompletedTask.ConfigureAwait(false);
            yield return "ok";
        }
    }

    /// <summary>Emits one chunk, then blocks until released or cancelled.</summary>
    private sealed class BlockingClient : ILlmClient
    {
        private readonly TaskCompletionSource _emitted = new();
        private readonly TaskCompletionSource _gate = new();

        public Task EmittedFirstChunk => _emitted.Task;

        public void Release() => _gate.TrySetResult();

        public async IAsyncEnumerable<string> StreamAsync(
            IReadOnlyList<ChatMessage> messages,
            string systemPrompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return "partial";
            _emitted.TrySetResult();

            using (cancellationToken.Register(() => _gate.TrySetResult()))
            {
                await _gate.Task.ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}

using OnScreenChat.Core.Data;
using OnScreenChat.Core.Llm;

namespace OnScreenChat.Core.Tests;

public class ModelOptionTests
{
    [Fact]
    public void Ships_the_two_gemini_models()
    {
        Assert.Equal(
            ["gemini:gemini-2.5-flash", "gemini:gemini-2.5-pro"],
            ModelOption.All.Select(option => option.Id));
    }

    [Fact]
    public void The_windows_build_offers_no_local_models()
    {
        Assert.All(ModelOption.All, option => Assert.Equal(ModelProvider.Gemini, option.Provider));
    }

    [Fact]
    public void Defaults_to_flash()
    {
        Assert.Equal("gemini:gemini-2.5-flash", ModelOption.Default.Id);
    }

    [Theory]
    [InlineData("gemini:gemini-2.5-pro", "gemini:gemini-2.5-pro")]
    [InlineData("ollama:qwen3:30b", "gemini:gemini-2.5-flash")] // a setting carried over from macOS
    [InlineData("nonsense", "gemini:gemini-2.5-flash")]
    [InlineData(null, "gemini:gemini-2.5-flash")]
    public void An_unknown_saved_id_falls_back_to_the_default(string? saved, string expected)
    {
        Assert.Equal(expected, ModelOption.Option(saved).Id);
    }

    [Fact]
    public void Every_option_has_a_picker_label()
    {
        Assert.All(ModelOption.All, option => Assert.StartsWith("Gemini · ", option.Label));
    }
}

public class GeminiClientTests
{
    [Fact]
    public void Endpoint_requests_server_sent_events_for_the_selected_model()
    {
        var client = new GeminiClient("gemini-2.5-pro", "key");
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-pro:streamGenerateContent?alt=sse",
            client.Endpoint.ToString());
    }

    [Fact]
    public void The_api_key_is_never_placed_in_the_url()
    {
        // It travels in the x-goog-api-key header so it can't leak via logs.
        var client = new GeminiClient("gemini-2.5-flash", "super-secret-key");
        Assert.DoesNotContain("super-secret-key", client.Endpoint.ToString());
    }
}

public class EchoClientTests
{
    private static readonly ChatMessage[] Conversation = [new(ChatRole.User, "hello there")];

    [Fact]
    public async Task Streams_the_reply_in_chunks()
    {
        var chunks = await Collect(new EchoClient(TimeSpan.Zero));
        Assert.True(chunks.Count > 1, "the point of the echo client is to exercise streaming");
    }

    [Fact]
    public async Task Chunks_reassemble_into_the_reply_and_quote_the_input()
    {
        var chunks = await Collect(new EchoClient(TimeSpan.Zero));
        Assert.Contains("I received: \"hello there\"", string.Concat(chunks));
    }

    [Fact]
    public async Task Reports_whether_a_skill_is_active()
    {
        var plain = string.Concat(await Collect(new EchoClient(TimeSpan.Zero)));
        var skilled = string.Concat(await Collect(new EchoClient(TimeSpan.Zero), "SOME SKILL"));

        Assert.Contains("Plain chat", plain);
        Assert.Contains("skill active", skilled);
    }

    [Fact]
    public async Task Cancellation_stops_the_stream()
    {
        using var cancellation = new CancellationTokenSource();
        var client = new EchoClient(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var chunk in client.StreamAsync(Conversation, "", cancellation.Token))
            {
                await cancellation.CancelAsync(); // stop after the first chunk
            }
        });
    }

    private static async Task<List<string>> Collect(ILlmClient client, string systemPrompt = "")
    {
        var chunks = new List<string>();
        await foreach (var chunk in client.StreamAsync(Conversation, systemPrompt))
        {
            chunks.Add(chunk);
        }
        return chunks;
    }
}

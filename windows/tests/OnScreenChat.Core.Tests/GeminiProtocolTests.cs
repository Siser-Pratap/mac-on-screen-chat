using System.Text.Json.Nodes;
using OnScreenChat.Core.Data;
using OnScreenChat.Core.Llm;

namespace OnScreenChat.Core.Tests;

public class GeminiProtocolTests
{
    // MARK: - Request shape

    [Fact]
    public void Assistant_turns_are_relabelled_as_model()
    {
        var body = Body([new ChatMessage(ChatRole.Assistant, "hi")]);
        Assert.Equal("model", body["contents"]![0]!["role"]!.GetValue<string>());
    }

    [Fact]
    public void User_turns_keep_their_role()
    {
        var body = Body([new ChatMessage(ChatRole.User, "hello")]);
        Assert.Equal("user", body["contents"]![0]!["role"]!.GetValue<string>());
        Assert.Equal("hello", body["contents"]![0]!["parts"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void Local_notes_never_reach_the_model()
    {
        var body = Body([
            new ChatMessage(ChatRole.User, "hello"),
            new ChatMessage(ChatRole.Note, "Rule saved."),
        ]);
        Assert.Single(body["contents"]!.AsArray());
    }

    [Fact]
    public void The_empty_assistant_bubble_being_streamed_into_is_skipped()
    {
        var body = Body([
            new ChatMessage(ChatRole.User, "hello"),
            new ChatMessage(ChatRole.Assistant, ""),
        ]);
        Assert.Single(body["contents"]!.AsArray());
    }

    [Fact]
    public void System_prompt_travels_as_system_instruction()
    {
        var body = Body([new ChatMessage(ChatRole.User, "hi")], "BE BRIEF");
        Assert.Equal("BE BRIEF", body["system_instruction"]!["parts"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void No_system_prompt_means_no_system_instruction_field()
    {
        Assert.Null(Body([new ChatMessage(ChatRole.User, "hi")])["system_instruction"]);
    }

    [Fact]
    public void Prompt_text_with_quotes_and_newlines_is_escaped_not_broken()
    {
        var prompt = "Say \"hi\"\nthen stop\\done";
        var body = Body([new ChatMessage(ChatRole.User, "x")], prompt);
        Assert.Equal(prompt, body["system_instruction"]!["parts"]![0]!["text"]!.GetValue<string>());
    }

    // MARK: - Stream parsing

    [Fact]
    public void Extracts_the_text_from_a_payload()
    {
        const string json = """{"candidates":[{"content":{"parts":[{"text":"Hello"}]}}]}""";
        Assert.Equal(["Hello"], GeminiProtocol.ExtractTexts(json));
    }

    [Fact]
    public void Extracts_every_part_in_order()
    {
        const string json = """{"candidates":[{"content":{"parts":[{"text":"a"},{"text":"b"}]}}]}""";
        Assert.Equal(["a", "b"], GeminiProtocol.ExtractTexts(json));
    }

    [Theory]
    [InlineData("""{"candidates":[]}""")]
    [InlineData("""{"candidates":[{"content":{}}]}""")]
    [InlineData("""{"promptFeedback":{"blockReason":"SAFETY"}}""")]
    [InlineData("not json at all")]
    [InlineData("")]
    public void A_payload_without_usable_text_yields_nothing_rather_than_throwing(string json)
    {
        // One malformed chunk must not kill a reply that is otherwise arriving.
        Assert.Empty(GeminiProtocol.ExtractTexts(json));
    }

    [Fact]
    public void A_non_string_text_field_is_ignored()
    {
        const string json = """{"candidates":[{"content":{"parts":[{"text":42}]}}]}""";
        Assert.Empty(GeminiProtocol.ExtractTexts(json));
    }

    // MARK: - SSE framing

    [Fact]
    public void Reads_the_payload_out_of_a_data_line()
    {
        Assert.True(GeminiProtocol.TryReadPayload("data: {\"a\":1}", out var payload));
        Assert.Equal("{\"a\":1}", payload);
    }

    [Theory]
    [InlineData("data: [DONE]")]
    [InlineData("data:")]
    [InlineData("data:   ")]
    [InlineData(": keep-alive comment")]
    [InlineData("event: message")]
    [InlineData("")]
    public void Non_payload_lines_are_skipped(string line)
    {
        Assert.False(GeminiProtocol.TryReadPayload(line, out _));
    }

    // MARK: - Errors

    [Fact]
    public void Reads_the_reason_out_of_an_error_body()
    {
        const string body = """{"error":{"code":429,"message":"Quota exceeded"}}""";
        Assert.Equal("Quota exceeded", GeminiProtocol.ErrorReason(body));
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("{}")]
    [InlineData("""{"error":{}}""")]
    public void An_unreadable_error_body_yields_no_reason(string body)
    {
        Assert.Null(GeminiProtocol.ErrorReason(body));
    }

    [Fact]
    public void Rate_limit_message_names_the_model_and_the_reason()
    {
        var message = GeminiProtocol.ErrorMessage(429, "Quota exceeded", "gemini-2.5-pro");
        Assert.Contains("HTTP 429", message);
        Assert.Contains("gemini-2.5-pro", message);
        Assert.Contains("Quota exceeded", message);
        Assert.Contains("Your API key is fine", message);
    }

    [Fact]
    public void No_error_message_offers_a_local_model_as_a_fallback()
    {
        // The Mac app suggests falling back to Ollama. There is no local model
        // in the Windows build, so suggesting one would send users nowhere.
        var messages = new[]
        {
            GeminiProtocol.ErrorMessage(429, "Quota exceeded", "gemini-2.5-pro"),
            GeminiProtocol.ErrorMessage(400, null, "gemini-2.5-flash"),
            GeminiProtocol.ErrorMessage(401, null, "gemini-2.5-flash"),
            GeminiProtocol.ErrorMessage(404, null, "gemini-2.5-flash"),
            GeminiProtocol.ErrorMessage(500, null, "gemini-2.5-flash"),
            GeminiProtocol.MissingKeyMessage,
        };

        Assert.All(messages, message =>
        {
            Assert.DoesNotContain("Ollama", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("local model", message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Setup_advice_points_at_the_windows_build_script()
    {
        // Not build-app.sh — that file does not exist on Windows.
        Assert.Contains("build-app.ps1", GeminiProtocol.ErrorMessage(401, null, "m"));
        Assert.Contains("build-app.ps1", GeminiProtocol.MissingKeyMessage);
        Assert.DoesNotContain(".sh", GeminiProtocol.MissingKeyMessage);
    }

    [Theory]
    [InlineData(400, "rejected the request")]
    [InlineData(401, "auth failed")]
    [InlineData(403, "auth failed")]
    [InlineData(404, "model not found")]
    [InlineData(503, "HTTP 503")]
    public void Every_status_gets_its_own_wording(int status, string expected)
    {
        Assert.Contains(expected, GeminiProtocol.ErrorMessage(status, null, "gemini-2.5-flash"));
    }

    [Fact]
    public void A_missing_reason_leaves_no_empty_parentheses()
    {
        Assert.DoesNotContain("()", GeminiProtocol.ErrorMessage(400, null, "m"));
        Assert.DoesNotContain("()", GeminiProtocol.ErrorMessage(400, "   ", "m"));
    }

    private static JsonObject Body(ChatMessage[] messages, string systemPrompt = "") =>
        JsonNode.Parse(GeminiProtocol.BuildRequestBody(messages, systemPrompt))!.AsObject();
}

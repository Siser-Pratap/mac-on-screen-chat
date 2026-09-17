using System.Text.Json;
using System.Text.Json.Nodes;
using OnScreenChat.Core.Data;

namespace OnScreenChat.Core.Llm;

/// <summary>
/// The pure parts of talking to Gemini — request shape, SSE payload parsing,
/// and error wording — split out from the transport so they can be tested
/// without a network or an API key.
/// </summary>
public static class GeminiProtocol
{
    public const string SsePrefix = "data:";

    public static string BuildRequestBody(IReadOnlyList<ChatMessage> messages, string systemPrompt)
    {
        var contents = new JsonArray();

        // Local notes are UI-only and never sent. The empty assistant bubble
        // being streamed into is skipped too.
        foreach (var message in messages)
        {
            if (message.Role == ChatRole.Note || message.Text.Length == 0) continue;

            contents.Add(new JsonObject
            {
                ["role"] = message.Role == ChatRole.User ? "user" : "model", // Gemini uses "model"
                ["parts"] = new JsonArray(new JsonObject { ["text"] = message.Text }),
            });
        }

        var body = new JsonObject { ["contents"] = contents };

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            body["system_instruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = systemPrompt }),
            };
        }

        return body.ToJsonString();
    }

    /// <summary>
    /// Pulls the text parts out of one streamed payload. Anything unexpected
    /// yields nothing rather than throwing — a malformed chunk must not kill a
    /// reply that is otherwise arriving fine.
    /// </summary>
    public static IReadOnlyList<string> ExtractTexts(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);

            // Indexing straight to [0] throws on an empty array — which is what
            // a safety-blocked or otherwise contentless payload looks like.
            var candidates = node?["candidates"]?.AsArray();
            if (candidates is null || candidates.Count == 0) return [];

            var parts = candidates[0]?["content"]?["parts"]?.AsArray();
            if (parts is null) return [];

            var texts = new List<string>();
            foreach (var part in parts)
            {
                if (part?["text"]?.GetValue<string>() is { Length: > 0 } text)
                {
                    texts.Add(text);
                }
            }
            return texts;
        }
        catch (JsonException)
        {
            return [];
        }
        catch (InvalidOperationException)
        {
            // A `text` field that isn't a string.
            return [];
        }
    }

    /// <summary>True for an SSE line that carries a payload worth parsing.</summary>
    public static bool TryReadPayload(string line, out string payload)
    {
        payload = string.Empty;
        if (!line.StartsWith(SsePrefix, StringComparison.Ordinal)) return false;

        var candidate = line[SsePrefix.Length..].Trim();
        if (candidate.Length == 0 || candidate == "[DONE]") return false;

        payload = candidate;
        return true;
    }

    /// <summary>Reads the human-readable reason out of an error response body.</summary>
    public static string? ErrorReason(string body)
    {
        try
        {
            return JsonNode.Parse(body)?["error"]?["message"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Builds the message shown in the transcript for a non-200. Worded for
    /// Windows: there is no local-model fallback to suggest.
    /// </summary>
    public static string ErrorMessage(int status, string? reason, string model)
    {
        var detail = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" ({reason})";

        return status switch
        {
            429 => $"⚠️ Gemini rate/quota limit hit (HTTP 429).{detail} The free tier for {model} is small — wait a bit, or switch to a lighter model like Gemini 2.5 Flash. Your API key is fine.",
            400 => $"⚠️ Gemini rejected the request (HTTP 400).{detail} The API key or request may be malformed.",
            401 or 403 => $"⚠️ Gemini auth failed (HTTP {status}).{detail} Check GEMINI_API_KEY in your .env, then run build-app.ps1 and relaunch.",
            404 => $"⚠️ Gemini model not found (HTTP 404).{detail} Check the model name \"{model}\".",
            _ => $"⚠️ Gemini HTTP {status}.{detail}",
        };
    }

    /// <summary>Shown when no API key is configured at all.</summary>
    public const string MissingKeyMessage =
        "⚠️ No Gemini API key found. Add GEMINI_API_KEY to your .env, run build-app.ps1, and relaunch. (See the README.)";
}

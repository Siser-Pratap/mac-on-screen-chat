using System.Runtime.CompilerServices;
using System.Text;
using OnScreenChat.Core.Data;

namespace OnScreenChat.Core.Llm;

/// <summary>
/// Streams from Google's Gemini API using Server-Sent Events. Requires an API
/// key (loaded from .env, never committed).
/// </summary>
public sealed class GeminiClient : ILlmClient
{
    private static readonly HttpClient SharedHttp = new()
    {
        // Long-running stream: the per-request timeout must not cut a reply off.
        Timeout = Timeout.InfiniteTimeSpan,
    };

    private readonly string _model;
    private readonly string _apiKey;
    private readonly HttpClient _http;

    public GeminiClient(string model, string apiKey, HttpClient? http = null)
    {
        _model = model;
        _apiKey = apiKey;
        _http = http ?? SharedHttp;
    }

    public Uri Endpoint => new(
        "https://generativelanguage.googleapis.com/v1beta/models/" +
        $"{_model}:streamGenerateContent?alt=sse");

    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        string systemPrompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // `yield` cannot appear inside a catch block, so every await that can
        // throw is wrapped in a helper that reports failure as a value instead.
        var (response, transportError) = await SendAsync(messages, systemPrompt, cancellationToken)
            .ConfigureAwait(false);

        if (transportError is not null)
        {
            yield return transportError;
            yield break;
        }

        if (response is null) yield break; // cancelled before the headers arrived

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                yield return await DescribeFailureAsync(response, cancellationToken).ConfigureAwait(false);
                yield break;
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (true)
            {
                var (line, ended) = await ReadLineAsync(reader, cancellationToken).ConfigureAwait(false);
                if (ended) yield break;
                if (!GeminiProtocol.TryReadPayload(line, out var payload)) continue;

                foreach (var text in GeminiProtocol.ExtractTexts(payload))
                {
                    yield return text;
                }
            }
        }
    }

    /// <summary>
    /// Sends the request, turning a cancellation into (null, null) and a
    /// transport failure into (null, message) so the iterator can yield it.
    /// </summary>
    private async Task<(HttpResponseMessage? Response, string? Error)> SendAsync(
        IReadOnlyList<ChatMessage> messages, string systemPrompt, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(
                    GeminiProtocol.BuildRequestBody(messages, systemPrompt),
                    Encoding.UTF8,
                    "application/json"),
            };
            request.Headers.Add("x-goog-api-key", _apiKey); // key in header, not URL

            var response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            return (response, null);
        }
        catch (OperationCanceledException)
        {
            return (null, null);
        }
        catch (HttpRequestException error)
        {
            return (null, $"⚠️ Couldn't reach Gemini. ({error.Message})");
        }
    }

    /// <summary>Reads one SSE line; `Ended` covers EOF, cancellation and a dropped connection.</summary>
    private static async Task<(string Line, bool Ended)> ReadLineAsync(
        StreamReader reader, CancellationToken cancellationToken)
    {
        try
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            return line is null ? (string.Empty, true) : (line, false);
        }
        catch (OperationCanceledException)
        {
            return (string.Empty, true);
        }
        catch (IOException)
        {
            // Connection dropped mid-reply; keep whatever already arrived.
            return (string.Empty, true);
        }
    }

    private async Task<string> DescribeFailureAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? reason = null;
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            reason = GeminiProtocol.ErrorReason(body);
        }
        catch (HttpRequestException)
        {
            // Fall through with no detail.
        }
        catch (OperationCanceledException)
        {
        }

        return GeminiProtocol.ErrorMessage((int)response.StatusCode, reason, _model);
    }
}

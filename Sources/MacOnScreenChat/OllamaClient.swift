import Foundation

/// Talks to a local Ollama server (`http://localhost:11434`). No API key, fully
/// on-device. Streams the `/api/chat` NDJSON response chunk-by-chunk.
struct OllamaClient: LLMClient {
    var model: String
    var endpoint: URL

    init(
        model: String = "qwen3:30b",
        endpoint: URL = URL(string: "http://localhost:11434/api/chat")!
    ) {
        self.model = model
        self.endpoint = endpoint
    }

    func stream(messages: [ChatMessage], systemPrompt: String) -> AsyncStream<String> {
        AsyncStream { continuation in
            let task = Task {
                do {
                    var request = URLRequest(url: endpoint)
                    request.httpMethod = "POST"
                    request.setValue("application/json", forHTTPHeaderField: "Content-Type")
                    request.httpBody = try JSONSerialization.data(
                        withJSONObject: requestBody(messages: messages, systemPrompt: systemPrompt)
                    )

                    let (bytes, response) = try await URLSession.shared.bytes(for: request)
                    guard let http = response as? HTTPURLResponse else {
                        continuation.yield("⚠️ No response from Ollama.")
                        continuation.finish()
                        return
                    }
                    guard http.statusCode == 200 else {
                        continuation.yield("⚠️ Ollama returned HTTP \(http.statusCode). Is the model \"\(model)\" pulled? Try `ollama pull \(model)`.")
                        continuation.finish()
                        return
                    }

                    // NDJSON: one JSON object per line.
                    for try await line in bytes.lines {
                        if Task.isCancelled { break }
                        guard let data = line.data(using: .utf8),
                              let obj = try JSONSerialization.jsonObject(with: data) as? [String: Any]
                        else { continue }

                        // Stream only the answer; ignore any `thinking` field.
                        if let message = obj["message"] as? [String: Any],
                           let content = message["content"] as? String,
                           !content.isEmpty {
                            continuation.yield(content)
                        }
                        if (obj["done"] as? Bool) == true { break }
                    }
                    continuation.finish()
                } catch is CancellationError {
                    continuation.finish()
                } catch {
                    continuation.yield("⚠️ Couldn't reach Ollama at \(endpoint.host ?? "localhost"). Is `ollama serve` running? (\(error.localizedDescription))")
                    continuation.finish()
                }
            }
            continuation.onTermination = { _ in task.cancel() }
        }
    }

    private func requestBody(messages: [ChatMessage], systemPrompt: String) -> [String: Any] {
        var apiMessages: [[String: String]] = []
        if !systemPrompt.isEmpty {
            apiMessages.append(["role": "system", "content": systemPrompt])
        }
        // Skip empty messages (e.g. the assistant placeholder being streamed into).
        for message in messages where !message.text.isEmpty {
            apiMessages.append([
                "role": message.role == .user ? "user" : "assistant",
                "content": message.text,
            ])
        }
        return [
            "model": model,
            "messages": apiMessages,
            "stream": true,
            "think": false, // skip reasoning latency; keeps bubbles clean
        ]
    }
}

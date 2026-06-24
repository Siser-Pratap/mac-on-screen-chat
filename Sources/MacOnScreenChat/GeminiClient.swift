import Foundation

/// Streams from Google's Gemini API (generativelanguage.googleapis.com) using
/// Server-Sent Events. Requires an API key (loaded from .env, never committed).
struct GeminiClient: LLMClient {
    var model: String
    var apiKey: String

    func stream(messages: [ChatMessage], systemPrompt: String) -> AsyncStream<String> {
        AsyncStream { continuation in
            let task = Task {
                do {
                    let endpoint = "https://generativelanguage.googleapis.com/v1beta/models/\(model):streamGenerateContent?alt=sse"
                    guard let url = URL(string: endpoint) else {
                        continuation.yield("⚠️ Bad Gemini endpoint.")
                        continuation.finish()
                        return
                    }

                    var request = URLRequest(url: url)
                    request.httpMethod = "POST"
                    request.setValue("application/json", forHTTPHeaderField: "Content-Type")
                    request.setValue(apiKey, forHTTPHeaderField: "x-goog-api-key") // key in header, not URL
                    request.httpBody = try JSONSerialization.data(
                        withJSONObject: requestBody(messages: messages, systemPrompt: systemPrompt)
                    )

                    let (bytes, response) = try await URLSession.shared.bytes(for: request)
                    guard let http = response as? HTTPURLResponse else {
                        continuation.yield("⚠️ No response from Gemini.")
                        continuation.finish()
                        return
                    }
                    guard http.statusCode == 200 else {
                        continuation.yield("⚠️ Gemini HTTP \(http.statusCode). Check the API key in .env and the model name.")
                        continuation.finish()
                        return
                    }

                    // SSE: lines like `data: {json}`.
                    for try await line in bytes.lines {
                        if Task.isCancelled { break }
                        guard line.hasPrefix("data:") else { continue }
                        let payload = line.dropFirst("data:".count).trimmingCharacters(in: .whitespaces)
                        guard !payload.isEmpty, payload != "[DONE]",
                              let data = payload.data(using: .utf8),
                              let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
                        else { continue }

                        for text in extractTexts(obj) where !text.isEmpty {
                            continuation.yield(text)
                        }
                    }
                    continuation.finish()
                } catch is CancellationError {
                    continuation.finish()
                } catch {
                    continuation.yield("⚠️ Couldn't reach Gemini. (\(error.localizedDescription))")
                    continuation.finish()
                }
            }
            continuation.onTermination = { _ in task.cancel() }
        }
    }

    private func extractTexts(_ obj: [String: Any]) -> [String] {
        guard let candidates = obj["candidates"] as? [[String: Any]],
              let content = candidates.first?["content"] as? [String: Any],
              let parts = content["parts"] as? [[String: Any]]
        else { return [] }
        return parts.compactMap { $0["text"] as? String }
    }

    private func requestBody(messages: [ChatMessage], systemPrompt: String) -> [String: Any] {
        var contents: [[String: Any]] = []
        for message in messages where !message.text.isEmpty {
            contents.append([
                "role": message.role == .user ? "user" : "model", // Gemini uses "model"
                "parts": [["text": message.text]],
            ])
        }

        var body: [String: Any] = ["contents": contents]
        if !systemPrompt.isEmpty {
            body["system_instruction"] = ["parts": [["text": systemPrompt]]]
        }
        return body
    }
}

import Foundation

/// The seam between the UI and whatever produces replies. The chat layer is
/// written entirely against this protocol, so the real provider (likely Claude
/// API, decided in Phase 3) drops in without touching the UI.
protocol LLMClient: Sendable {
    /// Streams the assistant reply token-by-token (or chunk-by-chunk).
    func stream(messages: [ChatMessage], systemPrompt: String) -> AsyncStream<String>
}

/// Phase 2 placeholder: fakes a streamed reply so the transcript, autoscroll,
/// and busy state are testable with no network or API key. Replaced in Phase 3.
struct EchoClient: LLMClient {
    func stream(messages: [ChatMessage], systemPrompt: String) -> AsyncStream<String> {
        let lastUser = messages.last(where: { $0.role == .user })?.text ?? ""
        let skillNote = systemPrompt.isEmpty ? "Plain chat" : "skill active"
        let reply = """
        [mock · \(skillNote)] I received: "\(lastUser)". \
        Real model responses arrive in Phase 3 — this is just the streaming UI.
        """

        return AsyncStream { continuation in
            Task {
                for word in reply.split(separator: " ", omittingEmptySubsequences: false) {
                    continuation.yield(word + " ")
                    try? await Task.sleep(nanoseconds: 35_000_000) // ~35ms/word
                }
                continuation.finish()
            }
        }
    }
}

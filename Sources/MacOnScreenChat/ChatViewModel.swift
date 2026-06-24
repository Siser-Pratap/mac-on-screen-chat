import Foundation

@MainActor
final class ChatViewModel: ObservableObject {
    @Published var messages: [ChatMessage] = []
    @Published var input: String = ""
    @Published var isStreaming = false

    private let client: LLMClient
    private let database: AppDatabase
    private var streamTask: Task<Void, Never>?

    init(client: LLMClient = OllamaClient(), database: AppDatabase = .shared) {
        self.client = client
        self.database = database
        self.messages = (try? database.loadMessages()) ?? []
    }

    var canSend: Bool {
        !input.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && !isStreaming
    }

    func send(systemPrompt: String) {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, !isStreaming else { return }

        let userMessage = ChatMessage(role: .user, text: trimmed)
        messages.append(userMessage)
        try? database.appendMessage(role: .user, text: trimmed, sortOrder: messages.count - 1)
        input = ""

        let assistant = ChatMessage(role: .assistant, text: "")
        messages.append(assistant)
        let assistantID = assistant.id
        let assistantOrder = messages.count - 1
        isStreaming = true

        let snapshot = messages
        streamTask = Task { [weak self] in
            guard let self else { return }
            let stream = self.client.stream(messages: snapshot, systemPrompt: systemPrompt)
            for await chunk in stream {
                if let idx = self.messages.firstIndex(where: { $0.id == assistantID }) {
                    self.messages[idx].text += chunk
                }
            }
            // Persist the finished assistant reply.
            if let idx = self.messages.firstIndex(where: { $0.id == assistantID }) {
                try? self.database.appendMessage(
                    role: .assistant, text: self.messages[idx].text, sortOrder: assistantOrder
                )
            }
            self.isStreaming = false
        }
    }

    func newChat() {
        streamTask?.cancel()
        streamTask = nil
        isStreaming = false
        messages.removeAll()
        input = ""
        try? database.clearMessages()
    }
}

import Foundation

@MainActor
final class ChatViewModel: ObservableObject {
    @Published var messages: [ChatMessage] = []
    @Published var input: String = ""
    @Published var isStreaming = false
    @Published var selectedModel: ModelOption {
        didSet { UserDefaults.standard.set(selectedModel.id, forKey: Self.modelKey) }
    }

    private let database: AppDatabase
    private var streamTask: Task<Void, Never>?
    private static let modelKey = "selectedModelID"

    init(database: AppDatabase = .shared) {
        self.database = database
        self.selectedModel = ModelOption.option(id: UserDefaults.standard.string(forKey: Self.modelKey))
        self.messages = (try? database.loadMessages()) ?? []
    }

    var canSend: Bool {
        !input.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && !isStreaming
    }

    func send(systemPrompt: String) {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, !isStreaming else { return }

        messages.append(ChatMessage(role: .user, text: trimmed))
        try? database.appendMessage(role: .user, text: trimmed, sortOrder: messages.count - 1)
        input = ""

        let assistant = ChatMessage(role: .assistant, text: "")
        messages.append(assistant)
        let assistantID = assistant.id
        let assistantOrder = messages.count - 1

        // Build the client for the selected provider.
        guard let client = makeClient(for: selectedModel, assistantID: assistantID, order: assistantOrder) else {
            return // makeClient already wrote an inline error + persisted it
        }

        isStreaming = true
        let snapshot = messages
        streamTask = Task { [weak self] in
            guard let self else { return }
            let stream = client.stream(messages: snapshot, systemPrompt: systemPrompt)
            for await chunk in stream {
                if let idx = self.messages.firstIndex(where: { $0.id == assistantID }) {
                    self.messages[idx].text += chunk
                }
            }
            if let idx = self.messages.firstIndex(where: { $0.id == assistantID }) {
                try? self.database.appendMessage(
                    role: .assistant, text: self.messages[idx].text, sortOrder: assistantOrder
                )
            }
            self.isStreaming = false
        }
    }

    /// Returns the client, or nil if it can't run (e.g. missing Gemini key) —
    /// in which case an inline error message is shown instead.
    private func makeClient(for model: ModelOption, assistantID: UUID, order: Int) -> LLMClient? {
        switch model.provider {
        case .ollama:
            return OllamaClient(model: model.modelName)
        case .gemini:
            let key = EnvLoader.value("GEMINI_API_KEY") ?? ""
            guard !key.isEmpty else {
                let note = "⚠️ No Gemini API key found. Add GEMINI_API_KEY to your .env, run ./build-app.sh, and relaunch. (See README.)"
                setAssistant(id: assistantID, text: note)
                try? database.appendMessage(role: .assistant, text: note, sortOrder: order)
                return nil
            }
            return GeminiClient(model: model.modelName, apiKey: key)
        }
    }

    private func setAssistant(id: UUID, text: String) {
        if let idx = messages.firstIndex(where: { $0.id == id }) {
            messages[idx].text = text
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

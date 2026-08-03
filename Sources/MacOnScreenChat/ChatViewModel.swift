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
    /// Bumped by every send and every stop, so a reply that finishes late can
    /// tell whether it's still the current one before touching shared state.
    private var generation = 0
    private static let modelKey = "selectedModelID"

    init(database: AppDatabase = .shared) {
        self.database = database
        self.selectedModel = ModelOption.option(id: UserDefaults.standard.string(forKey: Self.modelKey))
        self.messages = (try? database.loadMessages()) ?? []
    }

    var canSend: Bool {
        !input.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && !isStreaming
    }

    func send(systemPrompt: String, rules: RuleStore) {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, !isStreaming else { return }

        // `/command {...}` captures a STANDING rule: saved, applied to every
        // future reply, and kept out of the transcript entirely.
        let (afterCommands, commands) = Self.extractCommands(from: trimmed)
        let saved = commands.compactMap { rules.add($0) }

        // `WW:{...}` steers only THIS reply (overriding the skill's formatting)
        // and is likewise kept out of the transcript.
        let (afterDirective, directive) = Self.extractDirective(from: afterCommands)
        let messageText = afterDirective.trimmingCharacters(in: .whitespacesAndNewlines)

        // Commands with nothing else to ask: acknowledge, don't call the model.
        if messageText.isEmpty, !commands.isEmpty {
            appendNote(Self.savedRuleNote(saved: saved, attempted: commands.count))
            input = ""
            return
        }
        // A bare `WW:{...}` has no message to steer — send it as written rather
        // than firing an empty request.
        let outgoing = messageText.isEmpty ? trimmed : messageText

        messages.append(ChatMessage(role: .user, text: outgoing))
        try? database.appendMessage(role: .user, text: outgoing, sortOrder: messages.count - 1)
        input = ""

        if !saved.isEmpty {
            appendNote(Self.savedRuleNote(saved: saved, attempted: commands.count))
        }

        let assistant = ChatMessage(role: .assistant, text: "")
        messages.append(assistant)
        let assistantID = assistant.id
        let assistantOrder = messages.count - 1

        // Build the client for the selected provider.
        guard let client = makeClient(for: selectedModel, assistantID: assistantID, order: assistantOrder) else {
            return // makeClient already wrote an inline error + persisted it
        }

        let effectiveSystem = Self.systemPrompt(
            skill: systemPrompt, rules: rules.promptBlock, directive: directive
        )

        isStreaming = true
        generation += 1
        let generation = self.generation
        let snapshot = messages
        streamTask = Task { [weak self] in
            guard let self else { return }
            // Guarantees no `**` reaches the transcript, whatever the model does.
            var stripper = AsteriskStripper()
            let stream = client.stream(messages: snapshot, systemPrompt: effectiveSystem)
            for await chunk in stream {
                let clean = stripper.push(chunk)
                guard !clean.isEmpty else { continue }
                if let idx = self.messages.firstIndex(where: { $0.id == assistantID }) {
                    self.messages[idx].text += clean
                }
            }
            self.finish(
                assistantID: assistantID, order: assistantOrder,
                tail: stripper.flush(), generation: generation
            )
        }
    }

    /// Cancels a reply in flight, keeping whatever text already arrived.
    /// Cancelling the task terminates the `AsyncStream`, which cancels the
    /// underlying `URLSession` task via its `onTermination` handler.
    func stop() {
        guard isStreaming else { return }
        generation += 1 // the cancelled reply no longer owns the shared state
        streamTask?.cancel()
        streamTask = nil
        isStreaming = false
    }

    /// Ends a reply: tidies the text, drops a bubble that never received
    /// anything (a stop before the first token), and persists the result.
    /// A stopped reply still lands here to keep what it had, but by then it's a
    /// stale generation and must not clear the flags of whatever came next.
    private func finish(assistantID: UUID, order: Int, tail: String, generation: Int) {
        defer {
            if generation == self.generation {
                isStreaming = false
                streamTask = nil
            }
        }
        guard let idx = messages.firstIndex(where: { $0.id == assistantID }) else { return }

        let text = ResponseStyle.normalized(messages[idx].text + tail)
        guard !text.isEmpty else {
            messages.remove(at: idx)
            return
        }
        messages[idx].text = text
        try? database.appendMessage(role: .assistant, text: text, sortOrder: order)
    }

    /// Assembles the full system prompt in precedence order: general formatting,
    /// then the skill's own output shape, then my standing rules, then any
    /// directive for this one reply.
    nonisolated static func systemPrompt(skill: String, rules: String, directive: String?) -> String {
        var prompt = ResponseStyle.contract
        if !skill.isEmpty {
            prompt += "\n\n" + skill
        }
        prompt += rules
        if let directive, !directive.isEmpty {
            prompt += """
            \n
            HIGHEST-PRIORITY INSTRUCTION FROM ME for this reply — apply it and let \
            it override any conflicting formatting or style rules above:
            \(directive)
            """
        }
        return prompt
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

    /// Extracts `/command {...}` standing rules (case-insensitive) from the
    /// input. Returns the message with those spans removed, plus each rule's
    /// text in the order it was written.
    nonisolated static func extractCommands(from text: String) -> (message: String, commands: [String]) {
        let (message, captures) = extract(pattern: #"(?i)/command\s*\{([^}]*)\}"#, from: text)
        return (message, captures)
    }

    /// Extracts inline `WW:{...}` steering directives (case-insensitive) from the
    /// input. Returns the message with those spans removed, plus the combined
    /// directive text (nil if there were none).
    nonisolated static func extractDirective(from text: String) -> (message: String, directive: String?) {
        let (message, captures) = extract(pattern: #"(?i)WW:\s*\{([^}]*)\}"#, from: text)
        return (message, captures.isEmpty ? nil : captures.joined(separator: "\n"))
    }

    /// Shared machinery for the `{...}` markers: pulls every capture group out
    /// and returns the text with the whole matches removed.
    nonisolated private static func extract(pattern: String, from text: String) -> (String, [String]) {
        guard let regex = try? NSRegularExpression(pattern: pattern) else { return (text, []) }
        let ns = text as NSString
        let matches = regex.matches(in: text, range: NSRange(location: 0, length: ns.length))
        guard !matches.isEmpty else { return (text, []) }

        var captures: [String] = []
        for match in matches where match.numberOfRanges > 1 {
            let capture = ns.substring(with: match.range(at: 1))
                .trimmingCharacters(in: .whitespacesAndNewlines)
            if !capture.isEmpty { captures.append(capture) }
        }

        var message = text
        for match in matches.reversed() {
            if let range = Range(match.range, in: message) {
                message.removeSubrange(range)
            }
        }
        message = message.trimmingCharacters(in: .whitespacesAndNewlines)
        return (message, captures)
    }

    /// Wording for the transcript after `/command {…}` — including the case
    /// where a rule was a duplicate and nothing new was stored.
    private static func savedRuleNote(saved: [String], attempted: Int) -> String {
        guard !saved.isEmpty else {
            return attempted == 1
                ? "Already saved — that rule is in effect."
                : "Already saved — those rules are in effect."
        }
        let list = saved.map { "- \($0)" }.joined(separator: "\n")
        let lead = saved.count == 1
            ? "Rule saved. It applies to every reply from now on:"
            : "Rules saved. They apply to every reply from now on:"
        return "\(lead)\n\(list)"
    }

    private func appendNote(_ text: String) {
        messages.append(ChatMessage(role: .note, text: text))
        try? database.appendMessage(role: .note, text: text, sortOrder: messages.count - 1)
    }

    private func setAssistant(id: UUID, text: String) {
        if let idx = messages.firstIndex(where: { $0.id == id }) {
            messages[idx].text = text
        }
    }

    func newChat() {
        generation += 1
        streamTask?.cancel()
        streamTask = nil
        isStreaming = false
        messages.removeAll()
        input = ""
        try? database.clearMessages()
    }
}

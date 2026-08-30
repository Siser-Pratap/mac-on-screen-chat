import Foundation

/// Headless check of the data layer — run with `MacOnScreenChat --selftest`.
/// Exercises DB creation + skill seeding without starting the GUI.
enum SelfTest {
    static func run() {
        do {
            let db = try AppDatabase()
            let skills = try db.allSkills()
            print("✅ DB opened. Seeded \(skills.count) skills:")
            for skill in skills {
                print("  • [\(skill.id)] \(skill.name) — hint: \(skill.inputHint)")
            }

            // Round-trip an edit to prove save/upsert works.
            if var first = skills.first {
                let original = first.name
                first.name = original + " (edited)"
                try db.save(first)
                let reloaded = try db.allSkills().first { $0.id == first.id }
                print("✅ Edit round-trip: \(reloaded?.name ?? "?")")
                first.name = original
                try db.save(first) // restore
            }

            // Message persistence round-trip.
            try db.clearMessages()
            try db.appendMessage(role: .user, text: "hello", sortOrder: 0)
            try db.appendMessage(role: .assistant, text: "hi there", sortOrder: 1)
            let loaded = try db.loadMessages()
            print("✅ Messages persisted: \(loaded.count) (\(loaded.map { "\($0.role):\($0.text)" }.joined(separator: ", ")))")
            try db.clearMessages()
            print("✅ Cleared messages: now \(try db.loadMessages().count)")

            try checkRules(db)
            checkFormatting()
        } catch {
            print("❌ SelfTest failed: \(error)")
            exit(1)
        }
    }

    /// Dry-run of the input markers — `MacOnScreenChat --markers "<text>"`.
    /// Shows exactly what the app would strip, save, steer with, and send,
    /// without writing anything or calling a model.
    static func markers(_ text: String) {
        let input = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !input.isEmpty else {
            print("Usage: MacOnScreenChat --markers \"summarize this /command {no emojis} WW:{one line}\"")
            exit(1)
        }

        let (afterCommands, commands) = ChatViewModel.extractCommands(from: input)
        let (afterDirective, directive) = ChatViewModel.extractDirective(from: afterCommands)
        let message = afterDirective.trimmingCharacters(in: .whitespacesAndNewlines)

        print("Input          \(input)")
        print("")
        print("/command  →  standing rules, saved and applied to EVERY future reply")
        print(commands.isEmpty ? "   (none)" : commands.map { "   • \($0)" }.joined(separator: "\n"))
        print("WW:       →  steers THIS reply only, then discarded")
        print("   \(directive.map { "• \($0)" } ?? "(none)")")
        print("")

        if message.isEmpty, !commands.isEmpty {
            print("Sent to model  (nothing — rules only)")
            print("Action         saves the rule(s), shows a confirmation note, no request")
        } else {
            let outgoing = message.isEmpty ? input : message
            print("Sent to model  \"\(outgoing)\"")
            if message.isEmpty {
                print("Action         no message left to steer, so the raw text is sent as written")
            } else {
                print("Action         sends that text; markers above never reach the model as text")
            }
        }

        let saved = (try? AppDatabase().allRules()) ?? []
        print("")
        print("Rules already saved (\(saved.count)):")
        print(saved.isEmpty ? "   (none)" : saved.map { "   • \($0.text)" }.joined(separator: "\n"))
        print("")
        print("Dry run — nothing was written.")
    }

    /// Standing rules survive a round-trip through SQLite.
    private static func checkRules(_ db: AppDatabase) throws {
        let existing = try db.allRules()
        let probe = Rule(id: "selftest-rule", text: "keep it under five lines", sortOrder: 999)
        try db.save(probe)
        let saved = try db.allRules().first { $0.id == probe.id }
        expect(saved?.text == probe.text, "rule round-trip", got: saved?.text ?? "nil")
        try db.deleteRule(id: probe.id)
        expect(try db.allRules().count == existing.count, "rule delete", got: "\(try db.allRules().count)")
    }

    /// The formatting guarantees: no `**` survives, however the stream splits,
    /// and the markers are parsed out of the input correctly.
    private static func checkFormatting() {
        // Split at the worst possible points: mid-run, and a run spanning chunks.
        var stripper = AsteriskStripper()
        var streamed = ""
        for chunk in ["Hey ", "*", "*bold", "*", "*", " and ", "***loud***", " and 2*3"] {
            streamed += stripper.push(chunk)
        }
        streamed += stripper.flush()
        expect(streamed == "Hey bold and loud and 2*3", "asterisk stripping", got: streamed)
        expect(!ResponseStyle.stripAsterisks("a **b** c").contains("**"), "no ** remains", got: "ok")

        let (message, commands) = ChatViewModel.extractCommands(
            from: "/command {always be brief} summarize this /COMMAND {no emojis}"
        )
        expect(message == "summarize this", "command stripped from message", got: message)
        expect(commands == ["always be brief", "no emojis"], "commands captured", got: "\(commands)")

        let (body, directive) = ChatViewModel.extractDirective(from: "reply WW:{one line}")
        expect(body == "reply" && directive == "one line", "directive parsing", got: "\(body) / \(directive ?? "nil")")

        let messy = "One.\n\n\n\nTwo.   \n\nThree."
        expect(ResponseStyle.normalized(messy) == "One.\n\nTwo.\n\nThree.", "blank-line collapse", got: ResponseStyle.normalized(messy))
        expect(ResponseStyle.paragraphs(of: messy).count == 3, "paragraph split", got: "\(ResponseStyle.paragraphs(of: messy).count)")
    }

    private static func expect(_ condition: Bool, _ label: String, got: String) {
        if condition {
            print("✅ \(label)")
        } else {
            print("❌ \(label) — got: \(got)")
            exit(1)
        }
    }
}

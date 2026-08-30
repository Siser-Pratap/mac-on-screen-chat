import Foundation

/// UI-facing source of truth for standing rules (`/command {…}`), backed by
/// SQLite. Rules survive "New chat" and relaunches — that's the whole point of
/// them versus a one-shot `WW:{…}` directive.
@MainActor
final class RuleStore: ObservableObject {
    @Published private(set) var rules: [Rule] = []

    private let database: AppDatabase

    init(database: AppDatabase = .shared) {
        self.database = database
        reload()
    }

    func reload() {
        rules = (try? database.allRules()) ?? []
    }

    /// Saves a new rule. Blank text and exact duplicates (ignoring case) are
    /// dropped. Returns the stored text, or nil if nothing was added.
    @discardableResult
    func add(_ text: String) -> String? {
        let cleaned = ResponseStyle.stripAsterisks(text)
            .trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleaned.isEmpty else { return nil }
        guard !rules.contains(where: { $0.text.caseInsensitiveCompare(cleaned) == .orderedSame })
        else { return nil }

        let rule = Rule(
            id: UUID().uuidString,
            text: cleaned,
            sortOrder: (rules.map(\.sortOrder).max() ?? -1) + 1
        )
        try? database.save(rule)
        reload()
        return cleaned
    }

    func update(_ rule: Rule, text: String) {
        let cleaned = ResponseStyle.stripAsterisks(text)
            .trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleaned.isEmpty else { return delete(rule) }
        var updated = rule
        updated.text = cleaned
        try? database.save(updated)
        reload()
    }

    func delete(_ rule: Rule) {
        try? database.deleteRule(id: rule.id)
        reload()
    }

    func clear() {
        try? database.clearRules()
        reload()
    }

    /// The rules rendered for the system prompt. Empty string when there are
    /// none, so callers can append it unconditionally.
    var promptBlock: String {
        guard !rules.isEmpty else { return "" }
        let list = rules.enumerated()
            .map { "\($0.offset + 1). \($0.element.text)" }
            .joined(separator: "\n")
        return """


        MY STANDING RULES — I set these myself and they apply to EVERY reply. \
        They outrank the formatting and style guidance above wherever they \
        conflict; only follow-up instructions I give in this message rank higher:
        \(list)
        """
    }
}

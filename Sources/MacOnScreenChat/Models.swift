import Foundation
import GRDB

enum ChatRole {
    case user
    case assistant
}

struct ChatMessage: Identifiable, Equatable {
    let id = UUID()
    let role: ChatRole
    var text: String
}

/// Persisted form of a chat message (single rolling conversation).
struct MessageRecord: Codable, FetchableRecord, PersistableRecord {
    var id: String
    var role: String // "user" | "assistant"
    var text: String
    var sortOrder: Int

    static let databaseTableName = "message"
}

/// A "special prompt," persisted in SQLite (table `skill`) and editable at
/// runtime. `Skill.defaults` seeds the table on first launch.
struct Skill: Codable, Identifiable, Hashable, FetchableRecord, PersistableRecord {
    var id: String
    var name: String
    var inputHint: String
    var systemPrompt: String
    var sortOrder: Int

    static let databaseTableName = "skill"
}

extension Skill {
    static let defaults: [Skill] = [
        Skill(
            id: "plain",
            name: "Plain chat",
            inputHint: "Ask me anything…",
            systemPrompt: "",
            sortOrder: 0
        ),
        Skill(
            id: "connection",
            name: "Connection writer",
            inputHint: "Paste their message or profile blurb…",
            systemPrompt: """
            You write short, genuine LinkedIn-style connection messages. Analyze \
            the pasted text for a specific hook (a shared interest, a recent post, \
            their role). Output ONE message under 300 characters — warm, specific, \
            and non-salesy. No emojis unless the source text uses them.
            """,
            sortOrder: 1
        ),
        Skill(
            id: "reply",
            name: "Reply analyzer",
            inputHint: "Paste the message you received…",
            systemPrompt: """
            You analyze an incoming message and help craft a reply. First give a \
            one-line read of the sender's intent, tone, and any explicit asks. \
            Then draft three labeled reply options: Warm, Concise, and Formal.
            """,
            sortOrder: 2
        ),
        Skill(
            id: "tone",
            name: "Tone rewriter",
            inputHint: "Paste your draft to rewrite…",
            systemPrompt: """
            You rewrite the user's draft message in a clearer, friendlier tone \
            while preserving its meaning and intent. Return only the rewritten \
            message, nothing else.
            """,
            sortOrder: 3
        ),
    ]

    static let fallback = defaults[0]
}

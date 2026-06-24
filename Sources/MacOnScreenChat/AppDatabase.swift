import Foundation
import GRDB

/// Owns the on-disk SQLite database (Application Support/MacOnScreenChat/app.sqlite),
/// runs migrations, and seeds the default skills on first launch.
/// Immutable wrapper around a thread-safe GRDB `DatabaseQueue`, so it's safe to
/// share across actors.
final class AppDatabase: Sendable {
    let dbQueue: DatabaseQueue

    static let shared: AppDatabase = {
        do {
            return try AppDatabase()
        } catch {
            fatalError("Failed to open database: \(error)")
        }
    }()

    init() throws {
        let fm = FileManager.default
        let appSupport = try fm.url(
            for: .applicationSupportDirectory, in: .userDomainMask,
            appropriateFor: nil, create: true
        )
        let dir = appSupport.appendingPathComponent("MacOnScreenChat", isDirectory: true)
        try fm.createDirectory(at: dir, withIntermediateDirectories: true)

        dbQueue = try DatabaseQueue(path: dir.appendingPathComponent("app.sqlite").path)
        try migrator.migrate(dbQueue)
        try seedSkillsIfNeeded()
    }

    private var migrator: DatabaseMigrator {
        var migrator = DatabaseMigrator()
        migrator.registerMigration("v1.createSkill") { db in
            try db.create(table: "skill") { t in
                t.primaryKey("id", .text)
                t.column("name", .text).notNull()
                t.column("inputHint", .text).notNull()
                t.column("systemPrompt", .text).notNull()
                t.column("sortOrder", .integer).notNull()
            }
        }
        migrator.registerMigration("v2.createMessage") { db in
            try db.create(table: "message") { t in
                t.primaryKey("id", .text)
                t.column("role", .text).notNull()
                t.column("text", .text).notNull()
                t.column("sortOrder", .integer).notNull()
            }
        }
        return migrator
    }

    private func seedSkillsIfNeeded() throws {
        try dbQueue.write { db in
            guard try Skill.fetchCount(db) == 0 else { return }
            for skill in Skill.defaults {
                try skill.insert(db)
            }
        }
    }

    // MARK: - Skills

    func allSkills() throws -> [Skill] {
        try dbQueue.read { db in
            try Skill.order(Column("sortOrder")).fetchAll(db)
        }
    }

    /// Insert or update (upsert by primary key).
    func save(_ skill: Skill) throws {
        try dbQueue.write { db in
            try skill.save(db)
        }
    }

    // MARK: - Conversation (single rolling thread)

    func loadMessages() throws -> [ChatMessage] {
        try dbQueue.read { db in
            try MessageRecord.order(Column("sortOrder")).fetchAll(db).map {
                ChatMessage(role: $0.role == "user" ? .user : .assistant, text: $0.text)
            }
        }
    }

    func appendMessage(role: ChatRole, text: String, sortOrder: Int) throws {
        let record = MessageRecord(
            id: UUID().uuidString,
            role: role == .user ? "user" : "assistant",
            text: text,
            sortOrder: sortOrder
        )
        try dbQueue.write { db in try record.insert(db) }
    }

    func clearMessages() throws {
        _ = try dbQueue.write { db in try MessageRecord.deleteAll(db) }
    }
}

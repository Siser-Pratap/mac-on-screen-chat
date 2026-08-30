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
        // The "Dating reply" skill shipped after first launch, so the empty-table
        // auto-seed won't add it for existing installs. Insert it here, but only
        // if the user hasn't already created a skill with this id.
        migrator.registerMigration("v3.seedDatingSkill") { db in
            guard let dating = Skill.defaults.first(where: { $0.id == "dating" }),
                  try Skill.filter(key: dating.id).fetchCount(db) == 0
            else { return }
            try dating.insert(db)
        }
        // Phase-2 prompt tuning: re-sync the "Dating reply" prompt to the current
        // default. Only touches the systemPrompt of the seeded skill; the user can
        // still edit it freely afterward (migrations run once).
        migrator.registerMigration("v4.tuneDatingPrompt") { db in
            guard let dating = Skill.defaults.first(where: { $0.id == "dating" }),
                  var existing = try Skill.filter(key: dating.id).fetchOne(db)
            else { return }
            existing.systemPrompt = dating.systemPrompt
            existing.inputHint = dating.inputHint
            try existing.update(db)
        }
        // Standing rules captured from `/command {…}`.
        migrator.registerMigration("v5.createRule") { db in
            try db.create(table: "rule") { t in
                t.primaryKey("id", .text)
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

    // MARK: - Standing rules

    func allRules() throws -> [Rule] {
        try dbQueue.read { db in
            try Rule.order(Column("sortOrder")).fetchAll(db)
        }
    }

    func save(_ rule: Rule) throws {
        try dbQueue.write { db in try rule.save(db) }
    }

    func deleteRule(id: String) throws {
        _ = try dbQueue.write { db in try Rule.deleteOne(db, key: id) }
    }

    func clearRules() throws {
        _ = try dbQueue.write { db in try Rule.deleteAll(db) }
    }

    // MARK: - Conversation (single rolling thread)

    func loadMessages() throws -> [ChatMessage] {
        try dbQueue.read { db in
            try MessageRecord.order(Column("sortOrder")).fetchAll(db).map {
                ChatMessage(role: ChatRole(rawValue: $0.role) ?? .assistant, text: $0.text)
            }
        }
    }

    func appendMessage(role: ChatRole, text: String, sortOrder: Int) throws {
        let record = MessageRecord(
            id: UUID().uuidString,
            role: role.rawValue,
            text: text,
            sortOrder: sortOrder
        )
        try dbQueue.write { db in try record.insert(db) }
    }

    func clearMessages() throws {
        _ = try dbQueue.write { db in try MessageRecord.deleteAll(db) }
    }
}

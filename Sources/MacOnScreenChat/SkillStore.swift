import Foundation

/// UI-facing source of truth for skills, backed by SQLite.
@MainActor
final class SkillStore: ObservableObject {
    @Published private(set) var skills: [Skill] = []

    private let database: AppDatabase

    init(database: AppDatabase = .shared) {
        self.database = database
        reload()
    }

    func reload() {
        skills = (try? database.allSkills()) ?? Skill.defaults
    }

    func save(_ skill: Skill) {
        try? database.save(skill)
        reload()
    }
}

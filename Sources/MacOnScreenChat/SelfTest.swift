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
        } catch {
            print("❌ SelfTest failed: \(error)")
            exit(1)
        }
    }
}

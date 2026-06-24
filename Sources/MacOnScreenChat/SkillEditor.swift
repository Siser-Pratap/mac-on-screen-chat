import SwiftUI

/// Edit a skill's name, input hint, and system prompt; saves back to SQLite.
struct SkillEditor: View {
    @State var skill: Skill
    let onSave: (Skill) -> Void
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Edit Skill")
                .font(.headline)

            Form {
                TextField("Name", text: $skill.name)
                TextField("Input hint", text: $skill.inputHint)

                VStack(alignment: .leading, spacing: 4) {
                    Text("System prompt").font(.caption).foregroundStyle(.secondary)
                    TextEditor(text: $skill.systemPrompt)
                        .font(.system(.body, design: .monospaced))
                        .frame(minHeight: 140)
                        .overlay(
                            RoundedRectangle(cornerRadius: 6)
                                .strokeBorder(.quaternary, lineWidth: 1)
                        )
                }
            }
            .formStyle(.grouped)

            HStack {
                Spacer()
                Button("Cancel") { dismiss() }
                    .keyboardShortcut(.cancelAction)
                Button("Save") {
                    onSave(skill)
                    dismiss()
                }
                .keyboardShortcut(.defaultAction)
                .disabled(skill.name.trimmingCharacters(in: .whitespaces).isEmpty)
            }
        }
        .padding(16)
        .frame(width: 460, height: 380)
    }
}

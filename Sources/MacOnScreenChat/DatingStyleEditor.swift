import SwiftUI

/// Edit the "My texting style" block for the Dating reply skill. It's stored
/// separately from the skill prompt (in UserDefaults) so it survives prompt
/// re-tuning and is a focused, personal input. Injected at send time so all
/// three reply options come out in the user's own voice.
struct DatingStyleEditor: View {
    @Binding var style: String
    @Environment(\.dismiss) private var dismiss
    @State private var draft: String = ""

    private static let placeholder = """
    Describe how you actually text so the replies sound like you. For example:
    • Tone/humor: dry and sarcastic, self-deprecating, lots of teasing
    • Length: short, one line, rarely more than a sentence
    • Style: all lowercase, no periods, occasional "haha"/"lol"
    • Emojis: barely any, maybe a 😏 when teasing
    • Words I use / avoid: …
    """

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("My texting style")
                .font(.headline)
            Text("Applies to the Dating reply skill. Leave empty to let the model just match her energy.")
                .font(.caption)
                .foregroundStyle(.secondary)

            ZStack(alignment: .topLeading) {
                if draft.isEmpty {
                    Text(Self.placeholder)
                        .font(.system(.callout))
                        .foregroundStyle(.tertiary)
                        .padding(.horizontal, 5)
                        .padding(.vertical, 8)
                        .allowsHitTesting(false)
                }
                TextEditor(text: $draft)
                    .font(.system(.body))
                    .scrollContentBackground(.hidden)
            }
            .frame(minHeight: 180)
            .overlay(
                RoundedRectangle(cornerRadius: 6)
                    .strokeBorder(.quaternary, lineWidth: 1)
            )

            HStack {
                Button("Clear", role: .destructive) { draft = "" }
                Spacer()
                Button("Cancel") { dismiss() }
                    .keyboardShortcut(.cancelAction)
                Button("Save") {
                    style = draft.trimmingCharacters(in: .whitespacesAndNewlines)
                    dismiss()
                }
                .keyboardShortcut(.defaultAction)
            }
        }
        .padding(16)
        .frame(width: 460, height: 380)
        .onAppear { draft = style }
    }
}

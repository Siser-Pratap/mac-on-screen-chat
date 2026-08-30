import SwiftUI

/// Lists the standing rules captured with `/command {…}` and lets me add, edit,
/// or remove them. Every rule here rides along with every reply.
struct RulesEditor: View {
    @ObservedObject var store: RuleStore
    @Environment(\.dismiss) private var dismiss
    @State private var draft = ""
    @FocusState private var draftFocused: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Standing rules")
                .font(.headline)
            Text("Applied to every reply, in every skill, until you remove them. Add one from the chat box with /command {…}.")
                .font(.caption)
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)

            list

            HStack(spacing: 8) {
                TextField("Add a rule…", text: $draft)
                    .textFieldStyle(.roundedBorder)
                    .focused($draftFocused)
                    .onSubmit(addDraft)
                Button("Add", action: addDraft)
                    .disabled(draft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }

            HStack {
                Button("Remove all") { store.clear() }
                    .disabled(store.rules.isEmpty)
                Spacer()
                Button("Done") { dismiss() }
                    .keyboardShortcut(.defaultAction)
            }
        }
        .padding(16)
        .frame(width: 460, height: 380)
        .onAppear { draftFocused = true }
    }

    private var list: some View {
        ScrollView {
            if store.rules.isEmpty {
                Text("No rules yet.")
                    .font(.callout)
                    .foregroundStyle(.tertiary)
                    .frame(maxWidth: .infinity, alignment: .center)
                    .padding(.vertical, 24)
            } else {
                VStack(spacing: 6) {
                    ForEach(store.rules) { rule in
                        RuleRow(rule: rule, store: store)
                    }
                }
                .padding(.vertical, 4)
            }
        }
        .frame(maxWidth: .infinity)
        .overlay(
            RoundedRectangle(cornerRadius: 8).strokeBorder(.quaternary, lineWidth: 1)
        )
    }

    private func addDraft() {
        store.add(draft)
        draft = ""
        draftFocused = true
    }
}

/// One rule: editable in place, committed on blur or Return.
private struct RuleRow: View {
    let rule: Rule
    @ObservedObject var store: RuleStore
    @State private var text: String = ""

    var body: some View {
        HStack(alignment: .top, spacing: 8) {
            Image(systemName: "checkmark.seal")
                .font(.caption)
                .foregroundStyle(.tint)
                .padding(.top, 4)

            TextField("", text: $text, axis: .vertical)
                .textFieldStyle(.plain)
                .onSubmit { commit() }
                .onChange(of: rule.text) { text = $0 }

            Button {
                store.delete(rule)
            } label: {
                Image(systemName: "trash")
                    .font(.caption)
            }
            .buttonStyle(.plain)
            .foregroundStyle(.secondary)
            .help("Delete rule")
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 7)
        .background(.quaternary.opacity(0.5), in: RoundedRectangle(cornerRadius: 8))
        .padding(.horizontal, 6)
        .onAppear { text = rule.text }
        .onDisappear { commit() }
    }

    private func commit() {
        guard text != rule.text else { return }
        store.update(rule, text: text)
    }
}

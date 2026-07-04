import SwiftUI

struct ContentView: View {
    @StateObject private var vm = ChatViewModel()
    @StateObject private var skillStore = SkillStore()
    @State private var selectedSkill: Skill = .fallback
    @State private var editingSkill: Skill?
    @State private var editingStyle = false
    @AppStorage("datingHeat") private var datingHeatRaw = DatingHeat.auto.rawValue
    @AppStorage("datingStyle") private var datingStyle = ""
    @FocusState private var inputFocused: Bool

    private var datingHeat: DatingHeat { DatingHeat(rawValue: datingHeatRaw) ?? .auto }

    /// Whether the dating-only controls (heat dial, "My style") apply to the
    /// current skill.
    private var showsHeatDial: Bool { selectedSkill.id == DatingHeat.skillID }

    /// The system prompt actually sent: the skill's prompt, plus — for the dating
    /// skill — my texting-style block and the heat calibration.
    private var effectivePrompt: String {
        guard showsHeatDial else { return selectedSkill.systemPrompt }
        var prompt = selectedSkill.systemPrompt
        let style = datingStyle.trimmingCharacters(in: .whitespacesAndNewlines)
        if !style.isEmpty {
            prompt += "\n\nMY TEXTING STYLE (write all three options in THIS voice — my humor, slang, and message length):\n\(style)"
        }
        prompt += datingHeat.promptDirective
        return prompt
    }

    var body: some View {
        VStack(spacing: 0) {
            header
            Divider().opacity(0.4)
            transcript
            if showsHeatDial { heatBar }
            inputBar
        }
        .frame(minWidth: 360, minHeight: 420)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 16))
        .overlay(
            RoundedRectangle(cornerRadius: 16)
                .strokeBorder(.white.opacity(0.12), lineWidth: 1)
        )
        .onAppear {
            inputFocused = true
            if let first = skillStore.skills.first { selectedSkill = first }
        }
        .sheet(item: $editingSkill) { skill in
            SkillEditor(skill: skill) { updated in
                skillStore.save(updated)
                if updated.id == selectedSkill.id { selectedSkill = updated }
            }
        }
        .sheet(isPresented: $editingStyle) {
            DatingStyleEditor(style: $datingStyle)
        }
    }

    // MARK: - Header

    private var header: some View {
        HStack(spacing: 8) {
            Menu {
                ForEach(skillStore.skills) { skill in
                    Button(skill.name) { selectedSkill = skill }
                }
                Divider()
                if showsHeatDial {
                    Button("My texting style…") { editingStyle = true }
                }
                Button("Edit “\(selectedSkill.name)”…") { editingSkill = selectedSkill }
            } label: {
                HStack(spacing: 4) {
                    Image(systemName: "wand.and.stars")
                    Text(selectedSkill.name)
                    Image(systemName: "chevron.down").font(.caption2)
                }
            }
            .menuStyle(.borderlessButton)
            .fixedSize()

            Spacer()

            Menu {
                ForEach(ModelOption.all) { option in
                    Button {
                        vm.selectedModel = option
                    } label: {
                        if option.id == vm.selectedModel.id {
                            Label(option.label, systemImage: "checkmark")
                        } else {
                            Text(option.label)
                        }
                    }
                }
            } label: {
                Image(systemName: "cpu")
            }
            .menuStyle(.borderlessButton)
            .fixedSize()
            .help("Model: \(vm.selectedModel.label)")

            Button {
                vm.newChat()
                inputFocused = true
            } label: {
                Image(systemName: "square.and.pencil")
            }
            .buttonStyle(.borderless)
            .help("New chat")
            .disabled(vm.messages.isEmpty && vm.input.isEmpty)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
    }

    // MARK: - Transcript

    private var transcript: some View {
        ScrollViewReader { proxy in
            ScrollView {
                if vm.messages.isEmpty {
                    emptyState
                        .padding(.top, 28)
                        .padding(.horizontal, 16)
                } else {
                    LazyVStack(spacing: 10) {
                        ForEach(vm.messages) { message in
                            MessageBubble(message: message, isStreaming: vm.isStreaming)
                                .id(message.id)
                        }
                        Color.clear.frame(height: 1).id(scrollAnchor)
                    }
                    .padding(12)
                }
            }
            .onChange(of: vm.messages) { _ in
                withAnimation(.easeOut(duration: 0.15)) {
                    proxy.scrollTo(scrollAnchor, anchor: .bottom)
                }
            }
        }
    }

    private let scrollAnchor = "bottom-anchor"

    private var emptyState: some View {
        VStack(spacing: 14) {
            Image(systemName: "bubble.left.and.bubble.right.fill")
                .font(.system(size: 28))
                .foregroundStyle(.tint)
            Text("On-Screen Chat")
                .font(.headline)
            Text("Pick a skill above, paste a message, and send.")
                .font(.callout)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)

            VStack(alignment: .leading, spacing: 6) {
                ForEach(skillStore.skills.filter { $0.id != "plain" }) { skill in
                    HStack(alignment: .top, spacing: 6) {
                        Image(systemName: "sparkle").font(.caption2).foregroundStyle(.tint)
                        VStack(alignment: .leading, spacing: 1) {
                            Text(skill.name).font(.caption).bold()
                            Text(skill.inputHint).font(.caption2).foregroundStyle(.secondary)
                        }
                    }
                }
            }
            .padding(.top, 4)
        }
    }

    // MARK: - Heat dial (Dating reply only)

    private var heatBar: some View {
        HStack(spacing: 8) {
            Image(systemName: "flame")
                .font(.caption)
                .foregroundStyle(.secondary)
                .help("How much she's into it — steers how far the replies escalate")
            Picker("Heat", selection: $datingHeatRaw) {
                ForEach(DatingHeat.allCases) { heat in
                    Text(heat.label).tag(heat.rawValue)
                }
            }
            .pickerStyle(.segmented)
            .labelsHidden()
        }
        .padding(.horizontal, 12)
        .padding(.top, 2)
    }

    // MARK: - Input bar

    private var inputBar: some View {
        HStack(alignment: .bottom, spacing: 8) {
            inputField

            Button {
                vm.send(systemPrompt: effectivePrompt)
            } label: {
                Image(systemName: "arrow.up.circle.fill")
                    .font(.system(size: 22))
            }
            .buttonStyle(.borderless)
            .keyboardShortcut(.return, modifiers: .command)
            .disabled(!vm.canSend)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
    }

    /// The text input. Plain Return sends; Shift+Return inserts a newline.
    @ViewBuilder
    private var inputField: some View {
        let field = TextField(selectedSkill.inputHint, text: $vm.input, axis: .vertical)
            .textFieldStyle(.plain)
            .lineLimit(1...6)
            .focused($inputFocused)
            .padding(.horizontal, 10)
            .padding(.vertical, 7)
            .background(.quaternary, in: RoundedRectangle(cornerRadius: 10))

        if #available(macOS 14.0, *) {
            field.onKeyPress(keys: [.return], phases: .down) { press in
                guard !press.modifiers.contains(.shift) else { return .ignored }
                if vm.canSend { vm.send(systemPrompt: effectivePrompt) }
                return .handled
            }
        } else {
            field
        }
    }
}

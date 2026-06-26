import SwiftUI
import AppKit

struct MessageBubble: View {
    let message: ChatMessage
    let isStreaming: Bool
    @State private var hovering = false
    @State private var copied = false

    var body: some View {
        HStack(alignment: .bottom, spacing: 6) {
            if message.role == .user { Spacer(minLength: 36) }

            bubble

            // Copy button for assistant replies, revealed on hover.
            if message.role == .assistant {
                copyButton
                    .opacity(message.text.isEmpty ? 0 : (hovering || copied ? 1 : 0.35))
                Spacer(minLength: 36)
            }
        }
        .onHover { hovering = $0 }
    }

    private var bubble: some View {
        Group {
            if message.role == .assistant && message.text.isEmpty && isStreaming {
                ProgressView().controlSize(.small)
            } else {
                Text(message.text)
                    .textSelection(.enabled)
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
        .background(background, in: RoundedRectangle(cornerRadius: 12))
        .foregroundStyle(message.role == .user ? Color.white : Color.primary)
    }

    private var copyButton: some View {
        Button {
            NSPasteboard.general.clearContents()
            NSPasteboard.general.setString(message.text, forType: .string)
            withAnimation(.easeOut(duration: 0.15)) { copied = true }
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.2) {
                withAnimation(.easeOut(duration: 0.2)) { copied = false }
            }
        } label: {
            Image(systemName: copied ? "checkmark" : "doc.on.doc")
                .font(.caption2)
                .frame(width: 20, height: 20)
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .foregroundStyle(copied ? Color.green : .secondary)
        .help("Copy reply")
    }

    private var background: AnyShapeStyle {
        message.role == .user
            ? AnyShapeStyle(Color.accentColor)
            : AnyShapeStyle(.quaternary)
    }
}

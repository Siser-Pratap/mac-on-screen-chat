import SwiftUI

struct ContentView: View {
    var body: some View {
        VStack(spacing: 14) {
            Image(systemName: "bubble.left.and.bubble.right.fill")
                .font(.system(size: 30))
                .foregroundStyle(.tint)

            Text("Mac On-Screen Chat")
                .font(.headline)

            VStack(spacing: 4) {
                Text("Phase 1 — floating panel is live.")
                Text("⌘⇧Space toggles it · Esc hides · drag to move")
                    .foregroundStyle(.secondary)
            }
            .font(.callout)
            .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(24)
        // Rounded, translucent card — the panel itself is transparent.
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 16))
        .overlay(
            RoundedRectangle(cornerRadius: 16)
                .strokeBorder(.white.opacity(0.12), lineWidth: 1)
        )
    }
}

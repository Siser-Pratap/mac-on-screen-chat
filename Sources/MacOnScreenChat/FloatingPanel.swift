import AppKit

/// A borderless, always-on-top panel that floats above other apps — including
/// fullscreen ones — and can still receive keyboard input for the chat box.
final class FloatingPanel: NSPanel {
    /// Called when the user presses Esc inside the panel.
    var onCancel: (() -> Void)?

    init(contentRect: NSRect) {
        super.init(
            contentRect: contentRect,
            styleMask: [.nonactivatingPanel, .titled, .fullSizeContentView, .resizable],
            backing: .buffered,
            defer: false
        )

        // Float over normal windows AND over other apps' fullscreen spaces.
        level = .floating
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .moveToActiveSpace]

        isFloatingPanel = true
        hidesOnDeactivate = false          // stay put when another app is focused
        isMovableByWindowBackground = true // drag from anywhere

        // Chromeless look — SwiftUI draws the rounded background itself.
        titleVisibility = .hidden
        titlebarAppearsTransparent = true
        standardWindowButton(.closeButton)?.isHidden = true
        standardWindowButton(.miniaturizeButton)?.isHidden = true
        standardWindowButton(.zoomButton)?.isHidden = true
        backgroundColor = .clear
        isOpaque = false
        hasShadow = true
    }

    // A non-activating, chromeless panel must opt in to becoming key/main,
    // otherwise the text field can't receive keystrokes.
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { true }

    // Esc → hide.
    override func cancelOperation(_ sender: Any?) {
        onCancel?()
    }
}

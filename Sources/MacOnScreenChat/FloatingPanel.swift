import AppKit

/// A borderless, always-on-top panel that floats above other apps — including
/// fullscreen ones — and can still receive keyboard input for the chat box.
final class FloatingPanel: NSPanel {
    /// Called when the user presses Esc inside the panel.
    var onCancel: (() -> Void)?

    /// When `true`, the panel is excluded from all screen capture / screen
    /// sharing (Zoom, Meet, Teams, QuickTime, OBS, `screencapture`) while
    /// staying fully visible on the local display. See `visibility.md`.
    var isHiddenFromScreenShare: Bool {
        get { sharingType == SharingType.none }
        set { sharingType = newValue ? SharingType.none : .readOnly }
    }

    init(contentRect: NSRect) {
        super.init(
            contentRect: contentRect,
            styleMask: [.nonactivatingPanel, .titled, .fullSizeContentView, .resizable],
            backing: .buffered,
            defer: false
        )

        // Float over normal windows AND over other apps' fullscreen spaces.
        // NOTE: .canJoinAllSpaces and .moveToActiveSpace are mutually exclusive —
        // combining them makes setCollectionBehavior throw. Keep only the former.
        level = .floating
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]

        // Exclude this window from ALL screen capture / screen sharing. The
        // window server never hands our pixels to another process, so the panel
        // is invisible in Zoom / Meet / Teams / QuickTime / OBS / screencapture,
        // while staying fully visible on the local physical display.
        // Safe default; can be flipped at runtime via `isHiddenFromScreenShare`.
        sharingType = .none

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

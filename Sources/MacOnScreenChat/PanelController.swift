import AppKit
import SwiftUI

/// Owns the floating panel: show/hide/toggle and remembers its on-screen frame.
final class PanelController {
    private let panel: FloatingPanel
    private let frameKey = "panelFrame"

    init() {
        let defaultRect = NSRect(x: 0, y: 0, width: 380, height: 480)
        panel = FloatingPanel(contentRect: defaultRect)
        panel.contentView = NSHostingView(rootView: ContentView())
        panel.onCancel = { [weak self] in self?.hide() }

        restoreFrame(default: defaultRect)

        // Persist frame whenever the user drags or resizes the panel.
        let center = NotificationCenter.default
        center.addObserver(self, selector: #selector(saveFrame),
                           name: NSWindow.didMoveNotification, object: panel)
        center.addObserver(self, selector: #selector(saveFrame),
                           name: NSWindow.didResizeNotification, object: panel)
    }

    func toggle() {
        if panel.isVisible {
            hide()
        } else {
            show()
        }
    }

    func show() {
        panel.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    func hide() {
        panel.orderOut(nil)
    }

    // MARK: - Frame persistence

    private func restoreFrame(default defaultRect: NSRect) {
        if let saved = UserDefaults.standard.string(forKey: frameKey) {
            panel.setFrame(NSRectFromString(saved), display: false)
        } else {
            panel.setFrame(defaultRect, display: false)
            panel.center()
        }
    }

    @objc private func saveFrame() {
        UserDefaults.standard.set(NSStringFromRect(panel.frame), forKey: frameKey)
    }
}

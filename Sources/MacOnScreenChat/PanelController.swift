import AppKit
import SwiftUI

/// Owns the floating panel: show/hide/toggle and remembers its on-screen frame.
final class PanelController {
    private let panel: FloatingPanel
    private let frameKey = "panelFrame"

    init() {
        Log.write("[panel] init: creating FloatingPanel")
        let defaultRect = NSRect(x: 0, y: 0, width: 380, height: 480)
        panel = FloatingPanel(contentRect: defaultRect)
        Log.write("[panel] init: creating NSHostingView(ContentView)")
        panel.contentView = NSHostingView(rootView: ContentView())
        Log.write("[panel] init: hosting view set")
        panel.onCancel = { [weak self] in self?.hide() }

        restoreFrame(default: defaultRect)
        Log.write("[panel] init: frame restored")

        // Persist frame whenever the user drags or resizes the panel.
        let center = NotificationCenter.default
        center.addObserver(self, selector: #selector(saveFrame),
                           name: NSWindow.didMoveNotification, object: panel)
        center.addObserver(self, selector: #selector(saveFrame),
                           name: NSWindow.didResizeNotification, object: panel)
    }

    func toggle() {
        log("toggle (visible=\(panel.isVisible))")
        if panel.isVisible {
            hide()
        } else {
            show()
        }
    }

    func show() {
        ensureOnScreen()
        panel.makeKeyAndOrderFront(nil)
        panel.orderFrontRegardless()
        NSApp.activate(ignoringOtherApps: true)
        log("show at \(NSStringFromRect(panel.frame))")
    }

    func hide() {
        panel.orderOut(nil)
        log("hide")
    }

    /// If the saved frame ended up off every screen, recenter so it can't get lost.
    private func ensureOnScreen() {
        let onScreen = NSScreen.screens.contains { $0.visibleFrame.intersects(panel.frame) }
        if !onScreen {
            log("frame off-screen, recentering")
            panel.center()
        }
    }

    private func log(_ message: String) {
        Log.write("[panel] \(message)")
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

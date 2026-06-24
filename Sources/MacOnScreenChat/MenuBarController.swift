import AppKit

/// A menu-bar status item — the app has no Dock icon (it's an `.accessory`
/// app), so this is how you toggle the panel and quit.
@MainActor
final class MenuBarController {
    private let statusItem: NSStatusItem
    private let onToggle: () -> Void

    init(onToggle: @escaping () -> Void) {
        self.onToggle = onToggle
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)

        if let button = statusItem.button {
            button.image = NSImage(
                systemSymbolName: "bubble.left.and.bubble.right.fill",
                accessibilityDescription: "On-Screen Chat"
            )
        }

        let menu = NSMenu()

        let toggleItem = NSMenuItem(
            title: "Show / Hide  (⌘⇧Space)",
            action: #selector(toggleTapped), keyEquivalent: ""
        )
        toggleItem.target = self
        menu.addItem(toggleItem)

        menu.addItem(.separator())

        let quitItem = NSMenuItem(
            title: "Quit On-Screen Chat", action: #selector(quitTapped), keyEquivalent: "q"
        )
        quitItem.target = self
        menu.addItem(quitItem)

        statusItem.menu = menu
    }

    @objc private func toggleTapped() { onToggle() }
    @objc private func quitTapped() { NSApp.terminate(nil) }
}

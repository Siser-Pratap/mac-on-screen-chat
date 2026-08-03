import AppKit

/// A menu-bar status item — the app has no Dock icon (it's an `.accessory`
/// app), so this is how you toggle the panel and quit.
@MainActor
final class MenuBarController {
    private let statusItem: NSStatusItem
    private let onToggle: () -> Void
    /// Flips the hide-from-screen-share setting and returns the new value.
    private let onToggleHideFromShare: () -> Bool
    private let hideItem: NSMenuItem

    init(
        hiddenFromScreenShare: Bool,
        onToggle: @escaping () -> Void,
        onToggleHideFromShare: @escaping () -> Bool
    ) {
        self.onToggle = onToggle
        self.onToggleHideFromShare = onToggleHideFromShare
        hideItem = NSMenuItem(
            title: "Hide from Screen Sharing",
            action: #selector(hideFromShareTapped), keyEquivalent: ""
        )
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

        // Privacy toggle — checkmark reflects whether the panel is excluded
        // from screen capture. See `visibility.md`.
        hideItem.target = self
        hideItem.state = hiddenFromScreenShare ? .on : .off
        menu.addItem(hideItem)

        menu.addItem(.separator())

        let quitItem = NSMenuItem(
            title: "Quit On-Screen Chat", action: #selector(quitTapped), keyEquivalent: "q"
        )
        quitItem.target = self
        menu.addItem(quitItem)

        statusItem.menu = menu
    }

    @objc private func toggleTapped() { onToggle() }

    @objc private func hideFromShareTapped() {
        let hidden = onToggleHideFromShare()
        hideItem.state = hidden ? .on : .off
    }

    @objc private func quitTapped() { NSApp.terminate(nil) }
}

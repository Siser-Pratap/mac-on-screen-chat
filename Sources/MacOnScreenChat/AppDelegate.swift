import AppKit
import Carbon.HIToolbox

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var panelController: PanelController?
    private var hotKey: GlobalHotKey?
    private var menuBar: MenuBarController?

    func applicationDidFinishLaunching(_ notification: Notification) {
        Log.write("[app] didFinishLaunching; activationPolicy=\(NSApp.activationPolicy().rawValue) bundle=\(Bundle.main.bundleIdentifier ?? "none")")

        // Standard menu so Cut/Copy/Paste/Select All key equivalents reach the
        // focused text field. Without it, ⌘C/⌘V/⌘X/⌘A do nothing — AppKit
        // delivers those shortcuts through the Edit menu, not the field directly.
        NSApp.mainMenu = Self.makeMainMenu()

        let controller = PanelController()
        self.panelController = controller

        // ⌘⇧Space toggles the floating panel, system-wide.
        hotKey = GlobalHotKey(
            keyCode: UInt32(kVK_Space),
            modifiers: UInt32(cmdKey | shiftKey)
        ) { [weak controller] in
            controller?.toggle()
        }
        Log.write("[app] hotKey registered=\(hotKey != nil)")

        // Menu-bar icon: the only persistent way to quit / toggle (no Dock icon).
        menuBar = MenuBarController { [weak controller] in
            controller?.toggle()
        }
        Log.write("[app] menuBar created")

        // Show on launch so it's immediately discoverable.
        controller.show()
        Log.write("[app] launch complete")
    }

    /// Minimal main menu. The items target nil (first responder) so they travel
    /// the responder chain to whatever text field is focused — that's what makes
    /// ⌘C/⌘V/⌘X/⌘A and ⌘Z work inside the panel.
    private static func makeMainMenu() -> NSMenu {
        let mainMenu = NSMenu()

        // App menu (hosts Quit so ⌘Q works for this accessory app).
        let appItem = NSMenuItem()
        mainMenu.addItem(appItem)
        let appMenu = NSMenu()
        appItem.submenu = appMenu
        appMenu.addItem(
            withTitle: "Quit",
            action: #selector(NSApplication.terminate(_:)),
            keyEquivalent: "q"
        )

        // Edit menu — the part that fixes copy/paste.
        let editItem = NSMenuItem()
        mainMenu.addItem(editItem)
        let editMenu = NSMenu(title: "Edit")
        editItem.submenu = editMenu
        editMenu.addItem(withTitle: "Undo", action: Selector(("undo:")), keyEquivalent: "z")
        let redo = editMenu.addItem(withTitle: "Redo", action: Selector(("redo:")), keyEquivalent: "z")
        redo.keyEquivalentModifierMask = [.command, .shift]
        editMenu.addItem(.separator())
        editMenu.addItem(withTitle: "Cut", action: #selector(NSText.cut(_:)), keyEquivalent: "x")
        editMenu.addItem(withTitle: "Copy", action: #selector(NSText.copy(_:)), keyEquivalent: "c")
        editMenu.addItem(withTitle: "Paste", action: #selector(NSText.paste(_:)), keyEquivalent: "v")
        editMenu.addItem(withTitle: "Select All", action: #selector(NSText.selectAll(_:)), keyEquivalent: "a")

        return mainMenu
    }
}

import AppKit
import Carbon.HIToolbox

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var panelController: PanelController?
    private var hotKey: GlobalHotKey?
    private var menuBar: MenuBarController?

    func applicationDidFinishLaunching(_ notification: Notification) {
        Log.write("[app] didFinishLaunching; activationPolicy=\(NSApp.activationPolicy().rawValue) bundle=\(Bundle.main.bundleIdentifier ?? "none")")

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
}

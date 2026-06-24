import AppKit
import Carbon.HIToolbox

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var panelController: PanelController?
    private var hotKey: GlobalHotKey?

    func applicationDidFinishLaunching(_ notification: Notification) {
        let controller = PanelController()
        self.panelController = controller

        // ⌘⇧Space toggles the floating panel, system-wide.
        hotKey = GlobalHotKey(
            keyCode: UInt32(kVK_Space),
            modifiers: UInt32(cmdKey | shiftKey)
        ) { [weak controller] in
            controller?.toggle()
        }

        // Show on launch so it's immediately discoverable.
        controller.show()
    }
}

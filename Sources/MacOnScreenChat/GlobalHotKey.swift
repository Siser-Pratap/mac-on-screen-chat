import AppKit
import Carbon.HIToolbox

/// Registers a system-wide hotkey via Carbon's `RegisterEventHotKey`.
/// Works app-wide with **no Accessibility permission** required (unlike an
/// `NSEvent` global monitor).
final class GlobalHotKey {
    private var hotKeyRef: EventHotKeyRef?
    private var eventHandler: EventHandlerRef?
    private let action: () -> Void

    /// - Parameters:
    ///   - keyCode: a virtual key code (e.g. `kVK_Space`).
    ///   - modifiers: Carbon modifier mask (e.g. `cmdKey | shiftKey`).
    init?(keyCode: UInt32, modifiers: UInt32, action: @escaping () -> Void) {
        self.action = action

        var eventType = EventTypeSpec(
            eventClass: OSType(kEventClassKeyboard),
            eventKind: OSType(kEventHotKeyPressed)
        )

        let selfPtr = Unmanaged.passUnretained(self).toOpaque()
        let installStatus = InstallEventHandler(
            GetApplicationEventTarget(),
            { _, _, userData -> OSStatus in
                guard let userData else { return noErr }
                let me = Unmanaged<GlobalHotKey>.fromOpaque(userData).takeUnretainedValue()
                me.action()
                return noErr
            },
            1, &eventType, selfPtr, &eventHandler
        )
        guard installStatus == noErr else { return nil }

        // 'MOSC' signature so this hotkey is identifiable.
        let hotKeyID = EventHotKeyID(signature: OSType(0x4D4F5343), id: 1)
        let registerStatus = RegisterEventHotKey(
            keyCode, modifiers, hotKeyID,
            GetApplicationEventTarget(), 0, &hotKeyRef
        )
        guard registerStatus == noErr else { return nil }
    }

    deinit {
        if let hotKeyRef { UnregisterEventHotKey(hotKeyRef) }
        if let eventHandler { RemoveEventHandler(eventHandler) }
    }
}

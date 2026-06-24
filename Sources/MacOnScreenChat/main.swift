import AppKit

// Make AppKit re-raise (crash on) exceptions it would otherwise swallow,
// so we get the actual reason instead of a silent half-launch.
UserDefaults.standard.register(defaults: ["NSApplicationCrashOnExceptions": true])

// Surface any AppKit-swallowed Obj-C exception with full details.
NSSetUncaughtExceptionHandler { exception in
    Log.write("[uncaught] \(exception.name.rawValue): \(exception.reason ?? "")\n" +
              exception.callStackSymbols.joined(separator: "\n"))
}

// Headless data-layer check; skips the GUI.
if CommandLine.arguments.contains("--selftest") {
    SelfTest.run()
    exit(0)
}

// Entry point. Built as a SwiftPM executable (no .xcodeproj required).
// Runs as an .accessory app: no Dock icon, lives as a floating panel.
let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.accessory)
app.run()

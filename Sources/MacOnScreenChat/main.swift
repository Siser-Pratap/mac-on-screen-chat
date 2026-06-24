import AppKit

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

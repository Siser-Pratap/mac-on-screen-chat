import Foundation

/// Lightweight file logger so we can diagnose launch/hotkey/window issues
/// regardless of how the app was started (Terminal, `open`, Finder).
/// Writes to ~/Library/Application Support/MacOnScreenChat/debug.log
enum Log {
    static let url: URL = {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        let dir = base.appendingPathComponent("MacOnScreenChat", isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir.appendingPathComponent("debug.log")
    }()

    static func write(_ message: String) {
        let line = "\(ISO8601DateFormatter().string(from: Date()))  \(message)\n"
        let data = Data(line.utf8)
        if let handle = try? FileHandle(forWritingTo: url) {
            handle.seekToEndOfFile()
            handle.write(data)
            try? handle.close()
        } else {
            try? data.write(to: url)
        }
        // Also echo to stderr (visible when run from a terminal).
        FileHandle.standardError.write(data)
    }
}

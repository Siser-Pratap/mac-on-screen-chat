import Foundation

/// Reads secrets (e.g. GEMINI_API_KEY) from the process environment or a local
/// `.env` file — never bundled, never committed. Lookup order:
///   1. process environment
///   2. ~/Library/Application Support/MacOnScreenChat/.env   (used by the .app)
///   3. ./.env in the current directory                      (used by `swift run`)
enum EnvLoader {
    static func value(_ key: String) -> String? {
        if let v = ProcessInfo.processInfo.environment[key], !v.isEmpty {
            return v
        }
        for url in candidateFiles() {
            if let v = parse(url)[key], !v.isEmpty {
                return v
            }
        }
        return nil
    }

    private static func candidateFiles() -> [URL] {
        var urls: [URL] = []
        if let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first {
            urls.append(base.appendingPathComponent("MacOnScreenChat/.env"))
        }
        urls.append(URL(fileURLWithPath: FileManager.default.currentDirectoryPath).appendingPathComponent(".env"))
        return urls
    }

    private static func parse(_ url: URL) -> [String: String] {
        guard let text = try? String(contentsOf: url, encoding: .utf8) else { return [:] }
        var result: [String: String] = [:]
        for rawLine in text.split(whereSeparator: { $0 == "\n" || $0 == "\r" }) {
            let line = rawLine.trimmingCharacters(in: .whitespaces)
            guard !line.isEmpty, !line.hasPrefix("#"), let eq = line.firstIndex(of: "=") else { continue }
            let key = line[..<eq].trimmingCharacters(in: .whitespaces)
            var val = line[line.index(after: eq)...].trimmingCharacters(in: .whitespaces)
            if val.count >= 2, val.hasPrefix("\""), val.hasSuffix("\"") {
                val = String(val.dropFirst().dropLast())
            }
            result[key] = val
        }
        return result
    }
}

import Foundation

/// Everything that governs the *shape* of a reply: the formatting contract sent
/// with every request, the live filter that keeps `**` out of the stream, and
/// the whitespace tidy-up used when rendering.
enum ResponseStyle {
    /// Prepended to every system prompt, whatever the skill. Kept general on
    /// purpose — it ends by yielding to any exact output shape a skill defines.
    static let contract = """
    RESPONSE FORMAT — this governs every reply you write:
    - Plain text only. Never write two asterisks anywhere in a reply: no \
    **bold**, no *emphasis*, no _underscores_, no # headings, no backticks and \
    no code fences. Emphasis is carried by word choice, not by symbols.
    - Break the reply into short paragraphs of at most two or three sentences, \
    with exactly ONE blank line between paragraphs. Never return a wall of text.
    - When you list things, put each item on its own line starting with "- " \
    (hyphen, space), and keep one blank line between the list and the text \
    around it.
    - One blank line is the only separator: never two or more in a row, and \
    never trailing spaces at the end of a line.
    - Lead with the answer, then the detail. No preamble ("Sure!", "Certainly"), \
    no restating of my question, no sign-off.

    These rules cover spacing and characters. If the skill below specifies an \
    exact output shape — a fixed set of labeled lines, a character limit, a \
    single sentence — that shape wins: keep it exactly, and apply these rules \
    inside it.
    """

    /// Tidies a finished reply for display and storage: strips trailing spaces
    /// on each line, collapses runs of blank lines to one, trims the ends.
    static func normalized(_ text: String) -> String {
        let lines = text
            .replacingOccurrences(of: "\r\n", with: "\n")
            .components(separatedBy: "\n")
            .map { $0.replacingOccurrences(of: "[ \t]+$", with: "", options: .regularExpression) }

        var out: [String] = []
        for line in lines {
            // Skip a blank line that follows another blank line.
            if line.isEmpty, out.last?.isEmpty == true { continue }
            out.append(line)
        }
        return out.joined(separator: "\n").trimmingCharacters(in: .whitespacesAndNewlines)
    }

    /// Splits text into paragraph blocks (separated by blank lines) so the UI
    /// can put real space between them. Line breaks *within* a block are kept.
    static func paragraphs(of text: String) -> [String] {
        normalized(text)
            .components(separatedBy: "\n\n")
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
    }

    /// Convenience for one-shot text (rule text, pasted input) — the streaming
    /// path uses `AsteriskStripper` directly so it can span chunk boundaries.
    static func stripAsterisks(_ text: String) -> String {
        var stripper = AsteriskStripper()
        return stripper.push(text) + stripper.flush()
    }
}

/// Deletes Markdown emphasis runs (two or more `*`) from a token stream.
///
/// Chunks arrive split at arbitrary points — `"**"` can land as `"*"` then
/// `"*"` — so a run at the end of a chunk is held back until the next chunk
/// reveals how long it really is. Call `flush()` once the stream finishes.
///
/// A *lone* asterisk is passed through: it's more likely `2*3` or a glob than
/// formatting, and only `**` was asked to disappear.
struct AsteriskStripper {
    private var pending = 0 // trailing asterisks carried over from the last chunk

    mutating func push(_ chunk: String) -> String {
        var out = ""
        var run = pending
        pending = 0

        for character in chunk {
            if character == "*" {
                run += 1
                continue
            }
            out += Self.render(run: run)
            run = 0
            out.append(character)
        }

        pending = run // may still grow in the next chunk
        return out
    }

    /// Emits whatever run was still being counted when the stream ended.
    mutating func flush() -> String {
        defer { pending = 0 }
        return Self.render(run: pending)
    }

    private static func render(run: Int) -> String {
        run == 1 ? "*" : "" // 0 → nothing, 1 → kept, 2+ → dropped
    }
}

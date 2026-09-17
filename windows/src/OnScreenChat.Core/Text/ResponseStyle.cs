using System.Text.RegularExpressions;

namespace OnScreenChat.Core.Text;

/// <summary>
/// Everything that governs the <i>shape</i> of a reply: the formatting contract
/// sent with every request (in ResponseStyleContract.g.cs), the live filter that
/// keeps <c>**</c> out of the stream, and the whitespace tidy-up used when
/// rendering.
/// </summary>
public static partial class ResponseStyle
{
    [GeneratedRegex(@"[ \t]+$")]
    private static partial Regex TrailingSpace();

    /// <summary>
    /// Tidies a finished reply for display and storage: strips trailing spaces
    /// on each line, collapses runs of blank lines to one, trims the ends.
    /// </summary>
    public static string Normalized(string text)
    {
        var lines = text
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => TrailingSpace().Replace(line, string.Empty));

        var output = new List<string>();
        foreach (var line in lines)
        {
            // Skip a blank line that follows another blank line.
            if (line.Length == 0 && output.Count > 0 && output[^1].Length == 0) continue;
            output.Add(line);
        }

        return string.Join("\n", output).Trim();
    }

    /// <summary>
    /// Splits text into paragraph blocks (separated by blank lines) so the UI can
    /// put real space between them. Line breaks <i>within</i> a block are kept.
    /// </summary>
    public static IReadOnlyList<string> Paragraphs(string text) =>
        Normalized(text)
            .Split("\n\n")
            .Select(block => block.Trim())
            .Where(block => block.Length > 0)
            .ToList();

    /// <summary>
    /// Convenience for one-shot text (rule text, pasted input) — the streaming
    /// path uses <see cref="AsteriskStripper"/> directly so it can span chunk
    /// boundaries.
    /// </summary>
    public static string StripAsterisks(string text)
    {
        var stripper = new AsteriskStripper();
        return stripper.Push(text) + stripper.Flush();
    }
}

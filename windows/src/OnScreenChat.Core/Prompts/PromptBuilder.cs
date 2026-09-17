using System.Text.RegularExpressions;
using OnScreenChat.Core.Text;

namespace OnScreenChat.Core.Prompts;

/// <summary>
/// Assembles the system prompt and pulls the input markers out of what I typed.
/// Pure functions — no state, no I/O — so the precedence order and the marker
/// parsing are testable on their own.
/// </summary>
public static partial class PromptBuilder
{
    [GeneratedRegex(@"/command\s*\{([^}]*)\}", RegexOptions.IgnoreCase)]
    private static partial Regex CommandMarker();

    [GeneratedRegex(@"WW:\s*\{([^}]*)\}", RegexOptions.IgnoreCase)]
    private static partial Regex DirectiveMarker();

    /// <summary>
    /// Assembles the full system prompt in precedence order: general formatting,
    /// then the skill's own output shape, then my standing rules, then any
    /// directive for this one reply.
    /// </summary>
    public static string SystemPrompt(string skill, string rules, string? directive)
    {
        var prompt = ResponseStyle.Contract;

        if (!string.IsNullOrEmpty(skill))
        {
            prompt += "\n\n" + skill;
        }

        prompt += rules;

        if (!string.IsNullOrEmpty(directive))
        {
            prompt += "\n\nHIGHEST-PRIORITY INSTRUCTION FROM ME for this reply — apply it and let " +
                      "it override any conflicting formatting or style rules above:\n" + directive;
        }

        return prompt;
    }

    /// <summary>
    /// Extracts <c>/command {...}</c> standing rules (case-insensitive) from the
    /// input. Returns the message with those spans removed, plus each rule's text
    /// in the order it was written.
    /// </summary>
    public static (string Message, IReadOnlyList<string> Commands) ExtractCommands(string text) =>
        Extract(CommandMarker(), text);

    /// <summary>
    /// Extracts inline <c>WW:{...}</c> steering directives (case-insensitive) from
    /// the input. Returns the message with those spans removed, plus the combined
    /// directive text (null if there were none).
    /// </summary>
    public static (string Message, string? Directive) ExtractDirective(string text)
    {
        var (message, captures) = Extract(DirectiveMarker(), text);
        return (message, captures.Count == 0 ? null : string.Join("\n", captures));
    }

    /// <summary>
    /// Shared machinery for the <c>{...}</c> markers: pulls every capture group
    /// out and returns the text with the whole matches removed.
    /// </summary>
    private static (string Message, IReadOnlyList<string> Captures) Extract(Regex regex, string text)
    {
        var matches = regex.Matches(text);
        if (matches.Count == 0) return (text, []);

        var captures = new List<string>();
        foreach (Match match in matches)
        {
            var capture = match.Groups[1].Value.Trim();
            if (capture.Length > 0) captures.Add(capture);
        }

        var message = text;
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            message = message.Remove(matches[i].Index, matches[i].Length);
        }

        return (message.Trim(), captures);
    }
}

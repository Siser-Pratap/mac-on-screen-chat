using System.Text;

namespace OnScreenChat.Core.Text;

/// <summary>
/// Deletes Markdown emphasis runs (two or more <c>*</c>) from a token stream.
/// </summary>
/// <remarks>
/// Chunks arrive split at arbitrary points — <c>"**"</c> can land as <c>"*"</c>
/// then <c>"*"</c> — so a run at the end of a chunk is held back until the next
/// chunk reveals how long it really is. Call <see cref="Flush"/> once the stream
/// finishes.
/// <para>
/// A <i>lone</i> asterisk is passed through: it's more likely <c>2*3</c> or a
/// glob than formatting, and only <c>**</c> was asked to disappear.
/// </para>
/// </remarks>
public sealed class AsteriskStripper
{
    /// <summary>Trailing asterisks carried over from the last chunk.</summary>
    private int _pending;

    public string Push(string chunk)
    {
        var output = new StringBuilder(chunk.Length);
        var run = _pending;
        _pending = 0;

        foreach (var character in chunk)
        {
            if (character == '*')
            {
                run++;
                continue;
            }

            output.Append(Render(run));
            run = 0;
            output.Append(character);
        }

        _pending = run; // may still grow in the next chunk
        return output.ToString();
    }

    /// <summary>Emits whatever run was still being counted when the stream ended.</summary>
    public string Flush()
    {
        var rendered = Render(_pending);
        _pending = 0;
        return rendered;
    }

    private static string Render(int run) =>
        run == 1 ? "*" : string.Empty; // 0 → nothing, 1 → kept, 2+ → dropped
}

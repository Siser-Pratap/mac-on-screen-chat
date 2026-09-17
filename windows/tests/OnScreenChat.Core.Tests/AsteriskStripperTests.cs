using OnScreenChat.Core.Text;

namespace OnScreenChat.Core.Tests;

/// <summary>
/// The guarantee is absolute: no `**` reaches the transcript, whatever the model
/// emits and however the stream happens to be chopped up.
/// </summary>
public class AsteriskStripperTests
{
    [Fact]
    public void Drops_emphasis_runs_within_one_chunk()
    {
        Assert.Equal("bold", Push("**bold**"));
    }

    [Fact]
    public void Drops_a_run_split_across_chunk_boundaries()
    {
        // The whole reason the stripper is stateful: "**" can arrive as "*" + "*".
        var stripper = new AsteriskStripper();
        var output = stripper.Push("*") + stripper.Push("*bold*") + stripper.Push("*");
        Assert.Equal("bold", output + stripper.Flush());
    }

    [Fact]
    public void Keeps_a_lone_asterisk()
    {
        // More likely `2*3` or a glob than formatting — only `**` was asked to go.
        Assert.Equal("2*3", Push("2*3"));
    }

    [Fact]
    public void Keeps_a_lone_trailing_asterisk_only_once_flushed()
    {
        var stripper = new AsteriskStripper();
        Assert.Equal("hi ", stripper.Push("hi *")); // held back — might become `**`
        Assert.Equal("*", stripper.Flush());
    }

    [Fact]
    public void Drops_a_trailing_run_on_flush()
    {
        var stripper = new AsteriskStripper();
        Assert.Equal("hi ", stripper.Push("hi **"));
        Assert.Equal(string.Empty, stripper.Flush());
    }

    [Fact]
    public void Flush_resets_pending_state()
    {
        var stripper = new AsteriskStripper();
        stripper.Push("*");
        stripper.Flush();
        Assert.Equal("a", stripper.Push("a"));
    }

    [Theory]
    [InlineData("***", "")]
    [InlineData("****text", "text")]
    [InlineData("a**b**c", "abc")]
    [InlineData("no markers here", "no markers here")]
    public void Handles_runs_of_every_length(string input, string expected)
    {
        Assert.Equal(expected, Push(input));
    }

    [Fact]
    public void Matches_the_one_shot_convenience_helper()
    {
        Assert.Equal("bold", ResponseStyle.StripAsterisks("**bold**"));
    }

    private static string Push(string text)
    {
        var stripper = new AsteriskStripper();
        return stripper.Push(text) + stripper.Flush();
    }
}

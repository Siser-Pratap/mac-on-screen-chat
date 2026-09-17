using OnScreenChat.Core.Text;

namespace OnScreenChat.Core.Tests;

public class ResponseStyleTests
{
    [Fact]
    public void Collapses_runs_of_blank_lines_to_one()
    {
        Assert.Equal("a\n\nb", ResponseStyle.Normalized("a\n\n\n\nb"));
    }

    [Fact]
    public void Strips_trailing_whitespace_on_each_line()
    {
        Assert.Equal("a\nb", ResponseStyle.Normalized("a  \nb\t"));
    }

    [Fact]
    public void Trims_the_ends()
    {
        Assert.Equal("a", ResponseStyle.Normalized("\n\n  a  \n\n"));
    }

    [Fact]
    public void Normalizes_windows_line_endings()
    {
        Assert.Equal("a\n\nb", ResponseStyle.Normalized("a\r\n\r\nb"));
    }

    [Fact]
    public void Splits_paragraphs_on_blank_lines()
    {
        var paragraphs = ResponseStyle.Paragraphs("one\n\ntwo");
        Assert.Equal(["one", "two"], paragraphs);
    }

    [Fact]
    public void Keeps_line_breaks_inside_a_paragraph()
    {
        // A list is one block; the UI must not space its items apart like paragraphs.
        var paragraphs = ResponseStyle.Paragraphs("intro\n\n- one\n- two");
        Assert.Equal(["intro", "- one\n- two"], paragraphs);
    }

    [Fact]
    public void Drops_empty_blocks()
    {
        Assert.Equal(["a", "b"], ResponseStyle.Paragraphs("\n\na\n\n\n\nb\n\n"));
    }

    [Fact]
    public void Empty_text_yields_no_paragraphs()
    {
        Assert.Empty(ResponseStyle.Paragraphs("   \n\n  "));
    }

    [Fact]
    public void Contract_states_the_rules_the_prompts_depend_on()
    {
        Assert.StartsWith("RESPONSE FORMAT", ResponseStyle.Contract);
        Assert.Contains("Plain text only", ResponseStyle.Contract);
        Assert.Contains("exactly ONE blank line between paragraphs", ResponseStyle.Contract);
        // The contract must yield to a skill that defines an exact output shape.
        Assert.Contains("that shape wins", ResponseStyle.Contract);
    }
}

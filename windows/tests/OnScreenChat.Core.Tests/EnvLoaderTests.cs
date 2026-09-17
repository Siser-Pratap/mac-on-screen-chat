using OnScreenChat.Core.Config;

namespace OnScreenChat.Core.Tests;

public class EnvLoaderTests
{
    [Fact]
    public void Parses_key_value_pairs()
    {
        var parsed = EnvLoader.ParseText("GEMINI_API_KEY=abc123");
        Assert.Equal("abc123", parsed["GEMINI_API_KEY"]);
    }

    [Fact]
    public void Skips_comments_and_blank_lines()
    {
        var parsed = EnvLoader.ParseText("# a comment\n\nKEY=value\n");
        Assert.Equal(["KEY"], parsed.Keys);
    }

    [Fact]
    public void Strips_surrounding_double_quotes()
    {
        Assert.Equal("value", EnvLoader.ParseText("KEY=\"value\"")["KEY"]);
    }

    [Fact]
    public void Keeps_equals_signs_inside_the_value()
    {
        // Base64-ish keys routinely contain '='; only the first one separates.
        Assert.Equal("a=b=c", EnvLoader.ParseText("KEY=a=b=c")["KEY"]);
    }

    [Fact]
    public void Trims_whitespace_around_key_and_value()
    {
        Assert.Equal("value", EnvLoader.ParseText("  KEY  =  value  ")["KEY"]);
    }

    [Fact]
    public void Handles_windows_line_endings()
    {
        var parsed = EnvLoader.ParseText("A=1\r\nB=2");
        Assert.Equal("1", parsed["A"]);
        Assert.Equal("2", parsed["B"]);
    }

    [Fact]
    public void Ignores_lines_without_a_separator()
    {
        Assert.Empty(EnvLoader.ParseText("not a pair"));
    }

    [Fact]
    public void Missing_file_is_not_an_error()
    {
        Assert.Empty(EnvLoader.Parse(Path.Combine(Path.GetTempPath(), "definitely-not-here.env")));
    }

    [Fact]
    public void Process_environment_wins_over_files()
    {
        const string key = "ONSCREENCHAT_TEST_KEY";
        Environment.SetEnvironmentVariable(key, "from-process");
        try
        {
            Assert.Equal("from-process", EnvLoader.Value(key));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void Unknown_key_returns_null()
    {
        Assert.Null(EnvLoader.Value("ONSCREENCHAT_NOT_SET_ANYWHERE"));
    }
}

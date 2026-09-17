using OnScreenChat.Core.Prompts;
using OnScreenChat.Core.Text;

namespace OnScreenChat.Core.Tests;

public class PromptBuilderTests
{
    // MARK: - /command {…} — standing rules

    [Fact]
    public void Extracts_a_command_and_removes_it_from_the_message()
    {
        var (message, commands) = PromptBuilder.ExtractCommands("summarize this /command {no emojis}");
        Assert.Equal("summarize this", message);
        Assert.Equal(["no emojis"], commands);
    }

    [Fact]
    public void Command_marker_is_case_insensitive()
    {
        var (_, commands) = PromptBuilder.ExtractCommands("/COMMAND {no emojis}");
        Assert.Equal(["no emojis"], commands);
    }

    [Fact]
    public void Extracts_several_commands_in_written_order()
    {
        var (message, commands) = PromptBuilder.ExtractCommands("/command {first} keep me /command {second}");
        Assert.Equal("keep me", message);
        Assert.Equal(["first", "second"], commands);
    }

    [Fact]
    public void Empty_braces_capture_nothing()
    {
        var (message, commands) = PromptBuilder.ExtractCommands("hi /command {}");
        Assert.Equal("hi", message);
        Assert.Empty(commands);
    }

    [Fact]
    public void Text_without_markers_is_returned_untouched()
    {
        var (message, commands) = PromptBuilder.ExtractCommands("just a normal message");
        Assert.Equal("just a normal message", message);
        Assert.Empty(commands);
    }

    // MARK: - WW:{…} — one-shot directives

    [Fact]
    public void Extracts_a_directive_and_removes_it()
    {
        var (message, directive) = PromptBuilder.ExtractDirective("rewrite this WW:{one line}");
        Assert.Equal("rewrite this", message);
        Assert.Equal("one line", directive);
    }

    [Fact]
    public void Several_directives_are_joined_by_newline()
    {
        var (_, directive) = PromptBuilder.ExtractDirective("WW:{one line} WW:{no labels}");
        Assert.Equal("one line\nno labels", directive);
    }

    [Fact]
    public void No_directive_yields_null_not_empty()
    {
        var (_, directive) = PromptBuilder.ExtractDirective("plain message");
        Assert.Null(directive);
    }

    [Fact]
    public void Both_marker_kinds_strip_cleanly_from_one_input()
    {
        // The documented example from the Mac app's `--markers` dry run.
        const string input = "summarize this /command {no emojis} WW:{one line}";

        var (afterCommands, commands) = PromptBuilder.ExtractCommands(input);
        var (message, directive) = PromptBuilder.ExtractDirective(afterCommands);

        Assert.Equal("summarize this", message);
        Assert.Equal(["no emojis"], commands);
        Assert.Equal("one line", directive);
    }

    // MARK: - Assembly order

    [Fact]
    public void Prompt_opens_with_the_formatting_contract()
    {
        var prompt = PromptBuilder.SystemPrompt("SKILL", string.Empty, null);
        Assert.StartsWith(ResponseStyle.Contract, prompt);
    }

    [Fact]
    public void Precedence_runs_contract_then_skill_then_rules_then_directive()
    {
        var prompt = PromptBuilder.SystemPrompt("SKILL_BODY", "\n\nRULES_BODY", "DIRECTIVE_BODY");

        var skill = prompt.IndexOf("SKILL_BODY", StringComparison.Ordinal);
        var rules = prompt.IndexOf("RULES_BODY", StringComparison.Ordinal);
        var directive = prompt.IndexOf("DIRECTIVE_BODY", StringComparison.Ordinal);

        Assert.True(skill > 0 && skill < rules, "the skill must follow the contract and precede the rules");
        Assert.True(rules < directive, "a one-shot directive must come last so it outranks everything");
    }

    [Fact]
    public void Directive_is_introduced_as_the_highest_priority_instruction()
    {
        var prompt = PromptBuilder.SystemPrompt(string.Empty, string.Empty, "one line");
        Assert.EndsWith(
            "\n\nHIGHEST-PRIORITY INSTRUCTION FROM ME for this reply — apply it and let " +
            "it override any conflicting formatting or style rules above:\none line",
            prompt);
    }

    [Fact]
    public void Empty_skill_adds_no_separator()
    {
        Assert.Equal(ResponseStyle.Contract, PromptBuilder.SystemPrompt(string.Empty, string.Empty, null));
    }

    [Fact]
    public void Empty_directive_is_treated_as_absent()
    {
        var withEmpty = PromptBuilder.SystemPrompt("SKILL", string.Empty, string.Empty);
        var withNull = PromptBuilder.SystemPrompt("SKILL", string.Empty, null);
        Assert.Equal(withNull, withEmpty);
    }
}

using OnScreenChat.Core.Data;
using OnScreenChat.Core.Prompts;

namespace OnScreenChat.Core.Tests;

/// <summary>
/// The system prompts are the product. These assert the generated file (from
/// windows/tools/sync-prompts.py) actually carries what the Mac app ships —
/// a silent truncation here would be invisible until a reply came back wrong.
/// </summary>
public class SkillDefaultsTests
{
    [Fact]
    public void Ships_the_same_six_skills_in_the_same_order()
    {
        Assert.Equal(
            ["plain", "connection", "reply", "tone", "comment", "dating"],
            SkillDefaults.All.Select(skill => skill.Id));
    }

    [Fact]
    public void Sort_order_matches_position()
    {
        Assert.All(SkillDefaults.All.Index(), pair => Assert.Equal(pair.Index, pair.Item.SortOrder));
    }

    [Fact]
    public void Fallback_is_plain_chat()
    {
        Assert.Equal("plain", SkillDefaults.Fallback.Id);
    }

    [Fact]
    public void Every_skill_has_a_name_hint_and_prompt()
    {
        Assert.All(SkillDefaults.All, skill =>
        {
            Assert.False(string.IsNullOrWhiteSpace(skill.Name));
            Assert.False(string.IsNullOrWhiteSpace(skill.InputHint));
            Assert.True(skill.SystemPrompt.Length > 100, $"{skill.Id} prompt looks truncated");
        });
    }

    [Fact]
    public void Connection_writer_keeps_the_background_and_the_handles()
    {
        var prompt = Prompt("connection");
        Assert.Contains("YOUR BACKGROUND (this is ME — the sender):", prompt);
        Assert.Contains("LinkedIn: linkedin.com/in/siser | X: x.com/PratapSiser", prompt);
        Assert.Contains("under 300 characters", prompt);
    }

    [Fact]
    public void Reply_analyzer_keeps_its_three_fixed_labels()
    {
        Assert.Contains("Warm, Concise, and Formal", Prompt("reply"));
    }

    [Fact]
    public void Dating_skill_keeps_its_exact_output_shape()
    {
        var prompt = Prompt("dating");
        Assert.Contains("Playful: <text>", prompt);
        Assert.Contains("Flirty: <text>", prompt);
        Assert.Contains("Spicy: <text>", prompt);
        Assert.Contains("Never neg, guilt-trip, love-bomb", prompt);
    }

    [Fact]
    public void Line_continuations_were_joined_not_left_as_backslashes()
    {
        // Swift's `\`-continued lines must come through as flowing sentences.
        Assert.All(SkillDefaults.All, skill =>
            Assert.DoesNotContain("\\\n", skill.SystemPrompt));
    }

    [Fact]
    public void Multi_paragraph_prompts_kept_their_blank_lines()
    {
        Assert.Contains("\n\n", Prompt("connection"));
    }

    [Fact]
    public void The_dating_skill_id_matches_what_the_heat_dial_looks_for()
    {
        Assert.Contains(SkillDefaults.All, skill => skill.Id == DatingHeatExtensions.SkillId);
    }

    private static string Prompt(string id) => SkillDefaults.All.Single(skill => skill.Id == id).SystemPrompt;
}

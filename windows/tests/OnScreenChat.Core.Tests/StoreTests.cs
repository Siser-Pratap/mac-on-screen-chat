using OnScreenChat.Core.Data;

namespace OnScreenChat.Core.Tests;

public sealed class StoreTests : IDisposable
{
    private readonly string _directory;
    private readonly AppDatabase _database;

    public StoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"onscreenchat-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _database = new AppDatabase(Path.Combine(_directory, "app.sqlite"));
    }

    public void Dispose()
    {
        _database.Dispose();
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }

    // MARK: - SkillStore

    [Fact]
    public void Skill_store_loads_the_seeded_skills()
    {
        var store = new SkillStore(_database);
        Assert.Equal(SkillDefaults.All.Count, store.Skills.Count);
    }

    [Fact]
    public void Saving_a_skill_refreshes_the_store_and_raises_changed()
    {
        var store = new SkillStore(_database);
        var raised = 0;
        store.Changed += () => raised++;

        store.Save(store.Skills[0] with { Name = "Edited" });

        Assert.Equal("Edited", store.Skills[0].Name);
        Assert.Equal(1, raised);
    }

    // MARK: - RuleStore

    [Fact]
    public void Adding_a_rule_returns_the_stored_text()
    {
        var store = new RuleStore(_database);
        Assert.Equal("be brief", store.Add("be brief"));
        Assert.Equal(["be brief"], store.Rules.Select(rule => rule.Text));
    }

    [Fact]
    public void Added_rules_are_trimmed_and_stripped_of_emphasis()
    {
        var store = new RuleStore(_database);
        Assert.Equal("be brief", store.Add("  **be brief**  "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("**")]
    public void Blank_rules_are_dropped(string input)
    {
        var store = new RuleStore(_database);
        Assert.Null(store.Add(input));
        Assert.Empty(store.Rules);
    }

    [Fact]
    public void Duplicate_rules_are_rejected_ignoring_case()
    {
        var store = new RuleStore(_database);
        store.Add("be brief");
        Assert.Null(store.Add("BE BRIEF"));
        Assert.Single(store.Rules);
    }

    [Fact]
    public void Sort_order_increments_so_rules_keep_their_sequence()
    {
        var store = new RuleStore(_database);
        store.Add("first");
        store.Add("second");
        store.Add("third");
        Assert.Equal(["first", "second", "third"], store.Rules.Select(rule => rule.Text));
    }

    [Fact]
    public void Updating_a_rule_replaces_its_text()
    {
        var store = new RuleStore(_database);
        store.Add("be brief");
        store.Update(store.Rules[0], "be very brief");
        Assert.Equal(["be very brief"], store.Rules.Select(rule => rule.Text));
    }

    [Fact]
    public void Updating_a_rule_to_blank_deletes_it()
    {
        var store = new RuleStore(_database);
        store.Add("be brief");
        store.Update(store.Rules[0], "   ");
        Assert.Empty(store.Rules);
    }

    [Fact]
    public void Clearing_removes_every_rule()
    {
        var store = new RuleStore(_database);
        store.Add("one");
        store.Add("two");
        store.Clear();
        Assert.Empty(store.Rules);
    }

    [Fact]
    public void Rules_survive_a_reload_from_disk()
    {
        new RuleStore(_database).Add("be brief");
        Assert.Equal(["be brief"], new RuleStore(_database).Rules.Select(rule => rule.Text));
    }

    // MARK: - The prompt block

    [Fact]
    public void No_rules_means_nothing_is_appended_to_the_prompt()
    {
        Assert.Equal(string.Empty, new RuleStore(_database).PromptBlock);
    }

    [Fact]
    public void Prompt_block_numbers_the_rules_and_states_their_precedence()
    {
        var store = new RuleStore(_database);
        store.Add("be brief");
        store.Add("no emojis");

        var block = store.PromptBlock;

        Assert.StartsWith("\n\nMY STANDING RULES", block);
        Assert.Contains("1. be brief", block);
        Assert.Contains("2. no emojis", block);
        // Rules outrank the formatting contract but yield to a follow-up instruction.
        Assert.Contains("They outrank the formatting and style guidance above", block);
        Assert.Contains("only follow-up instructions I give in this message rank higher", block);
    }

    [Fact]
    public void Prompt_block_braces_are_not_mangled_by_formatting()
    {
        // The block is assembled with string.Format; a rule containing braces
        // must survive rather than throw or vanish.
        var store = new RuleStore(_database);
        store.Add("never output {placeholders}");
        Assert.Contains("never output {placeholders}", store.PromptBlock);
    }
}

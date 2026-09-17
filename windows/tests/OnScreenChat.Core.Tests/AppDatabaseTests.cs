using OnScreenChat.Core.Data;

namespace OnScreenChat.Core.Tests;

/// <summary>Each test gets its own database file, so nothing leaks between them.</summary>
public sealed class AppDatabaseTests : IDisposable
{
    private readonly string _directory;
    private readonly string _path;

    public AppDatabaseTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"onscreenchat-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "app.sqlite");
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }

    private AppDatabase Open() => new(_path);

    [Fact]
    public void Seeds_the_default_skills_on_first_open()
    {
        using var database = Open();
        Assert.Equal(
            SkillDefaults.All.Select(skill => skill.Id),
            database.AllSkills().Select(skill => skill.Id));
    }

    [Fact]
    public void Skills_come_back_in_sort_order()
    {
        using var database = Open();
        var orders = database.AllSkills().Select(skill => skill.SortOrder).ToList();
        Assert.Equal(orders.OrderBy(order => order), orders);
    }

    [Fact]
    public void Reopening_does_not_reseed_or_re_migrate()
    {
        // The whole point of the user_version counter: migrations run once.
        using (var first = Open())
        {
            first.Save(first.AllSkills()[0] with { Name = "Renamed" });
        }

        using var second = Open();
        Assert.Equal(SkillDefaults.All.Count, second.AllSkills().Count);
        Assert.Equal("Renamed", second.AllSkills()[0].Name);
    }

    [Fact]
    public void Saving_a_skill_upserts_rather_than_duplicating()
    {
        using var database = Open();
        var before = database.AllSkills().Count;
        var skill = database.AllSkills()[0];

        database.Save(skill with { Name = "Edited", SystemPrompt = "New prompt" });

        Assert.Equal(before, database.AllSkills().Count);
        var reloaded = database.AllSkills().Single(other => other.Id == skill.Id);
        Assert.Equal("Edited", reloaded.Name);
        Assert.Equal("New prompt", reloaded.SystemPrompt);
    }

    [Fact]
    public void Seeded_prompts_survive_the_round_trip_intact()
    {
        // A prompt truncated by the storage layer would be as bad as a bad port.
        using var database = Open();
        var stored = database.AllSkills().Single(skill => skill.Id == "dating");
        Assert.Equal(SkillDefaults.All.Single(skill => skill.Id == "dating").SystemPrompt, stored.SystemPrompt);
    }

    [Fact]
    public void Messages_round_trip_in_order()
    {
        using var database = Open();
        database.AppendMessage(ChatRole.User, "hello", 0);
        database.AppendMessage(ChatRole.Assistant, "hi there", 1);

        var loaded = database.LoadMessages();
        Assert.Equal(["hello", "hi there"], loaded.Select(message => message.Text));
        Assert.Equal([ChatRole.User, ChatRole.Assistant], loaded.Select(message => message.Role));
    }

    [Fact]
    public void Notes_keep_their_role_so_they_stay_out_of_requests()
    {
        using var database = Open();
        database.AppendMessage(ChatRole.Note, "Rule saved.", 0);
        Assert.Equal(ChatRole.Note, database.LoadMessages()[0].Role);
    }

    [Fact]
    public void Clearing_messages_leaves_skills_and_rules_alone()
    {
        using var database = Open();
        database.AppendMessage(ChatRole.User, "hello", 0);
        database.Save(new Rule { Id = "r1", Text = "be brief", SortOrder = 0 });

        database.ClearMessages();

        Assert.Empty(database.LoadMessages());
        Assert.Single(database.AllRules());
        Assert.Equal(SkillDefaults.All.Count, database.AllSkills().Count);
    }

    [Fact]
    public void Rules_round_trip_delete_and_clear()
    {
        using var database = Open();
        database.Save(new Rule { Id = "r1", Text = "be brief", SortOrder = 0 });
        database.Save(new Rule { Id = "r2", Text = "no emojis", SortOrder = 1 });
        Assert.Equal(["be brief", "no emojis"], database.AllRules().Select(rule => rule.Text));

        database.DeleteRule("r1");
        Assert.Equal(["no emojis"], database.AllRules().Select(rule => rule.Text));

        database.ClearRules();
        Assert.Empty(database.AllRules());
    }

    [Fact]
    public void Saving_a_rule_twice_updates_it()
    {
        using var database = Open();
        database.Save(new Rule { Id = "r1", Text = "be brief", SortOrder = 0 });
        database.Save(new Rule { Id = "r1", Text = "be very brief", SortOrder = 0 });

        Assert.Equal(["be very brief"], database.AllRules().Select(rule => rule.Text));
    }

    [Fact]
    public void Creates_the_database_directory_if_it_is_missing()
    {
        var nested = Path.Combine(_directory, "does", "not", "exist", "app.sqlite");
        using var database = new AppDatabase(nested);
        Assert.True(File.Exists(nested));
    }
}

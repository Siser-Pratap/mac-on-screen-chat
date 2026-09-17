namespace OnScreenChat.Core.Data;

/// <summary>
/// Source of truth for skills, backed by SQLite. Deliberately free of any UI
/// framework type — the WinUI layer subscribes to <see cref="Changed"/> and
/// projects this into an observable collection.
/// </summary>
public sealed class SkillStore
{
    private readonly AppDatabase _database;

    public SkillStore(AppDatabase database)
    {
        _database = database;
        Reload();
    }

    public IReadOnlyList<Skill> Skills { get; private set; } = [];

    public event Action? Changed;

    public void Reload()
    {
        Skills = _database.AllSkills();
        if (Skills.Count == 0) Skills = SkillDefaults.All;
        Changed?.Invoke();
    }

    public void Save(Skill skill)
    {
        _database.Save(skill);
        Reload();
    }
}

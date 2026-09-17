namespace OnScreenChat.Core.Data;

/// <summary>
/// A "special prompt," persisted in SQLite (table <c>skill</c>) and editable at
/// runtime. <see cref="SkillDefaults.All"/> seeds the table on first launch.
/// </summary>
public sealed record Skill
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string InputHint { get; init; }
    public required string SystemPrompt { get; init; }
    public required int SortOrder { get; init; }
}

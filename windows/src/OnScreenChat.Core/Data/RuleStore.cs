using OnScreenChat.Core.Prompts;
using OnScreenChat.Core.Text;

namespace OnScreenChat.Core.Data;

/// <summary>
/// Source of truth for standing rules (<c>/command {…}</c>), backed by SQLite.
/// Rules survive "New chat" and relaunches — that's the whole point of them
/// versus a one-shot <c>WW:{…}</c> directive.
/// </summary>
public sealed class RuleStore
{
    private readonly AppDatabase _database;

    public RuleStore(AppDatabase database)
    {
        _database = database;
        Reload();
    }

    public IReadOnlyList<Rule> Rules { get; private set; } = [];

    public event Action? Changed;

    public void Reload()
    {
        Rules = _database.AllRules();
        Changed?.Invoke();
    }

    /// <summary>
    /// Saves a new rule. Blank text and exact duplicates (ignoring case) are
    /// dropped. Returns the stored text, or null if nothing was added.
    /// </summary>
    public string? Add(string text)
    {
        var cleaned = ResponseStyle.StripAsterisks(text).Trim();
        if (cleaned.Length == 0) return null;
        if (Rules.Any(rule => string.Equals(rule.Text, cleaned, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        _database.Save(new Rule
        {
            Id = Guid.NewGuid().ToString(),
            Text = cleaned,
            SortOrder = Rules.Count == 0 ? 0 : Rules.Max(rule => rule.SortOrder) + 1,
        });
        Reload();
        return cleaned;
    }

    public void Update(Rule rule, string text)
    {
        var cleaned = ResponseStyle.StripAsterisks(text).Trim();
        if (cleaned.Length == 0)
        {
            Delete(rule);
            return;
        }

        _database.Save(rule with { Text = cleaned });
        Reload();
    }

    public void Delete(Rule rule)
    {
        _database.DeleteRule(rule.Id);
        Reload();
    }

    public void Clear()
    {
        _database.ClearRules();
        Reload();
    }

    /// <summary>
    /// The rules rendered for the system prompt. Empty string when there are
    /// none, so callers can append it unconditionally.
    /// </summary>
    public string PromptBlock
    {
        get
        {
            if (Rules.Count == 0) return string.Empty;

            var list = string.Join("\n", Rules.Select((rule, index) => $"{index + 1}. {rule.Text}"));
            return string.Format(PromptTemplates.StandingRules, list);
        }
    }
}

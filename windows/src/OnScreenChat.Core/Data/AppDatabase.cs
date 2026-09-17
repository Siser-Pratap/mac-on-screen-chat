using Microsoft.Data.Sqlite;
using OnScreenChat.Core.Config;

namespace OnScreenChat.Core.Data;

/// <summary>
/// Owns the on-disk SQLite database (<c>%LOCALAPPDATA%\OnScreenChat\app.sqlite</c>),
/// runs migrations, and seeds the default skills on first launch.
/// </summary>
/// <remarks>
/// The Mac app uses GRDB's <c>DatabaseQueue</c>, which serializes every access
/// through one connection. This mirrors that: a single connection guarded by a
/// lock, rather than a pool, so writes can't interleave.
/// </remarks>
public sealed class AppDatabase : IDisposable
{
    private static readonly Lock SharedGate = new();
    private static AppDatabase? _shared;

    private readonly SqliteConnection _connection;
    private readonly Lock _gate = new();

    /// <summary>The process-wide instance. Created on first use.</summary>
    public static AppDatabase Shared
    {
        get
        {
            lock (SharedGate)
            {
                return _shared ??= new AppDatabase();
            }
        }
    }

    public AppDatabase(string? databasePath = null)
    {
        var path = databasePath ?? AppPaths.DatabaseFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());
        _connection.Open();

        Migrate();
        SeedSkillsIfNeeded();
    }

    // MARK: - Migrations

    /// <summary>
    /// GRDB's named-migration ledger becomes a <c>user_version</c> counter. The
    /// Mac app's v1–v5 collapse into one step here: v3/v4 were retrofits that
    /// repaired existing Mac installs, and a fresh Windows install has no legacy
    /// rows to repair.
    /// </summary>
    private void Migrate()
    {
        var migrations = new Action<SqliteConnection>[]
        {
            CreateInitialSchema,
        };

        var version = ExecuteScalarInt("PRAGMA user_version");

        for (var index = version; index < migrations.Length; index++)
        {
            using var transaction = _connection.BeginTransaction();
            migrations[index](_connection);
            // PRAGMA can't be parameterized; the value is a loop counter, not input.
            Execute($"PRAGMA user_version = {index + 1}", transaction);
            transaction.Commit();
        }
    }

    private static void CreateInitialSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE skill (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                inputHint TEXT NOT NULL,
                systemPrompt TEXT NOT NULL,
                sortOrder INTEGER NOT NULL
            );
            CREATE TABLE message (
                id TEXT PRIMARY KEY,
                role TEXT NOT NULL,
                text TEXT NOT NULL,
                sortOrder INTEGER NOT NULL
            );
            CREATE TABLE rule (
                id TEXT PRIMARY KEY,
                text TEXT NOT NULL,
                sortOrder INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private void SeedSkillsIfNeeded()
    {
        lock (_gate)
        {
            if (ExecuteScalarInt("SELECT COUNT(*) FROM skill") > 0) return;

            using var transaction = _connection.BeginTransaction();
            foreach (var skill in SkillDefaults.All)
            {
                SaveSkill(skill, transaction);
            }
            transaction.Commit();
        }
    }

    // MARK: - Skills

    public IReadOnlyList<Skill> AllSkills()
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                "SELECT id, name, inputHint, systemPrompt, sortOrder FROM skill ORDER BY sortOrder";

            var skills = new List<Skill>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                skills.Add(new Skill
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    InputHint = reader.GetString(2),
                    SystemPrompt = reader.GetString(3),
                    SortOrder = reader.GetInt32(4),
                });
            }
            return skills;
        }
    }

    /// <summary>Insert or update (upsert by primary key).</summary>
    public void Save(Skill skill)
    {
        lock (_gate)
        {
            SaveSkill(skill, transaction: null);
        }
    }

    private void SaveSkill(Skill skill, SqliteTransaction? transaction)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO skill (id, name, inputHint, systemPrompt, sortOrder)
            VALUES ($id, $name, $hint, $prompt, $order)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                inputHint = excluded.inputHint,
                systemPrompt = excluded.systemPrompt,
                sortOrder = excluded.sortOrder
            """;
        command.Parameters.AddWithValue("$id", skill.Id);
        command.Parameters.AddWithValue("$name", skill.Name);
        command.Parameters.AddWithValue("$hint", skill.InputHint);
        command.Parameters.AddWithValue("$prompt", skill.SystemPrompt);
        command.Parameters.AddWithValue("$order", skill.SortOrder);
        command.ExecuteNonQuery();
    }

    // MARK: - Standing rules

    public IReadOnlyList<Rule> AllRules()
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT id, text, sortOrder FROM rule ORDER BY sortOrder";

            var rules = new List<Rule>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rules.Add(new Rule
                {
                    Id = reader.GetString(0),
                    Text = reader.GetString(1),
                    SortOrder = reader.GetInt32(2),
                });
            }
            return rules;
        }
    }

    public void Save(Rule rule)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO rule (id, text, sortOrder)
                VALUES ($id, $text, $order)
                ON CONFLICT(id) DO UPDATE SET
                    text = excluded.text,
                    sortOrder = excluded.sortOrder
                """;
            command.Parameters.AddWithValue("$id", rule.Id);
            command.Parameters.AddWithValue("$text", rule.Text);
            command.Parameters.AddWithValue("$order", rule.SortOrder);
            command.ExecuteNonQuery();
        }
    }

    public void DeleteRule(string id)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM rule WHERE id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
    }

    public void ClearRules()
    {
        lock (_gate)
        {
            Execute("DELETE FROM rule");
        }
    }

    // MARK: - Conversation (single rolling thread)

    public IReadOnlyList<ChatMessage> LoadMessages()
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT role, text FROM message ORDER BY sortOrder";

            var messages = new List<ChatMessage>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                messages.Add(new ChatMessage(ParseRole(reader.GetString(0)), reader.GetString(1)));
            }
            return messages;
        }
    }

    public void AppendMessage(ChatRole role, string text, int sortOrder)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                "INSERT INTO message (id, role, text, sortOrder) VALUES ($id, $role, $text, $order)";
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$role", RoleName(role));
            command.Parameters.AddWithValue("$text", text);
            command.Parameters.AddWithValue("$order", sortOrder);
            command.ExecuteNonQuery();
        }
    }

    public void ClearMessages()
    {
        lock (_gate)
        {
            Execute("DELETE FROM message");
        }
    }

    /// <summary>Stored lowercase, matching the Mac app's raw values.</summary>
    internal static string RoleName(ChatRole role) => role switch
    {
        ChatRole.User => "user",
        ChatRole.Note => "note",
        _ => "assistant",
    };

    /// <summary>Unknown roles read back as assistant, as on macOS.</summary>
    internal static ChatRole ParseRole(string raw) => raw switch
    {
        "user" => ChatRole.User,
        "note" => ChatRole.Note,
        _ => ChatRole.Assistant,
    };

    // MARK: - Plumbing

    private int ExecuteScalarInt(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private void Execute(string sql, SqliteTransaction? transaction = null)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}

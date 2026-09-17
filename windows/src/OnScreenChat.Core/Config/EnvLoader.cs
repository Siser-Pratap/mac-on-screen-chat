namespace OnScreenChat.Core.Config;

/// <summary>
/// Reads secrets (e.g. GEMINI_API_KEY) from the process environment or a local
/// <c>.env</c> file — never bundled, never committed. Lookup order:
/// <list type="number">
///   <item>process environment</item>
///   <item><c>%LOCALAPPDATA%\OnScreenChat\.env</c> (used by the installed app)</item>
///   <item><c>.\.env</c> in the current directory (used by <c>dotnet run</c>)</item>
/// </list>
/// </summary>
public static class EnvLoader
{
    public static string? Value(string key)
    {
        var fromProcess = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrEmpty(fromProcess)) return fromProcess;

        foreach (var file in CandidateFiles())
        {
            if (Parse(file).TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateFiles()
    {
        yield return AppPaths.EnvFile;
        yield return Path.Combine(Directory.GetCurrentDirectory(), ".env");
    }

    public static IReadOnlyDictionary<string, string> Parse(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, string>();

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException)
        {
            return new Dictionary<string, string>();
        }

        return ParseText(text);
    }

    public static IReadOnlyDictionary<string, string> ParseText(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var separator = line.IndexOf('=');
            if (separator < 0) continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
            {
                value = value[1..^1];
            }

            result[key] = value;
        }

        return result;
    }
}

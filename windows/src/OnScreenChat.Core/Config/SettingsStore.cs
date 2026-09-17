using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnScreenChat.Core.Config;

/// <summary>
/// The Windows stand-in for the Mac app's <c>UserDefaults</c>: a small JSON file
/// at <c>%LOCALAPPDATA%\OnScreenChat\settings.json</c>. Keys match the Mac ones
/// so the two apps stay easy to compare.
/// </summary>
/// <remarks>
/// Every setter writes through immediately — the app is dismissed with a hotkey
/// and quit from a tray menu, so there is no reliable "on exit" moment to flush
/// at. A corrupt or unreadable file is treated as empty rather than fatal.
/// </remarks>
public sealed class SettingsStore
{
    private const string FrameKey = "panelFrame";
    private const string HideFromShareKey = "hideFromScreenShare";
    private const string ModelKey = "selectedModelID";
    private const string HeatKey = "datingHeat";
    private const string StyleKey = "datingStyle";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly Lock _gate = new();
    private JsonObject _values;

    public SettingsStore(string? path = null)
    {
        _path = path ?? AppPaths.SettingsFile;
        _values = Load(_path);
    }

    /// <summary>The panel's last position, or null if it has never been moved.</summary>
    public PanelFrame? Frame
    {
        get => PanelFrame.TryParse(GetString(FrameKey));
        set => Set(FrameKey, value?.Serialize());
    }

    /// <summary>
    /// Defaults to <c>true</c>: the panel is excluded from screen capture out of
    /// the box, matching the Mac app's privacy-first default.
    /// </summary>
    public bool HideFromScreenShare
    {
        get => GetBool(HideFromShareKey) ?? true;
        set => Set(HideFromShareKey, value);
    }

    public string? SelectedModelId
    {
        get => GetString(ModelKey);
        set => Set(ModelKey, value);
    }

    public string DatingHeat
    {
        get => GetString(HeatKey) ?? "auto";
        set => Set(HeatKey, value);
    }

    public string DatingStyle
    {
        get => GetString(StyleKey) ?? string.Empty;
        set => Set(StyleKey, value);
    }

    // MARK: - Plumbing

    private string? GetString(string key)
    {
        lock (_gate)
        {
            return _values[key] is JsonValue value && value.TryGetValue<string>(out var text)
                ? text
                : null;
        }
    }

    private bool? GetBool(string key)
    {
        lock (_gate)
        {
            return _values[key] is JsonValue value && value.TryGetValue<bool>(out var flag)
                ? flag
                : null;
        }
    }

    private void Set(string key, string? value)
    {
        lock (_gate)
        {
            if (value is null) _values.Remove(key);
            else _values[key] = JsonValue.Create(value);
            Save();
        }
    }

    private void Set(string key, bool value)
    {
        lock (_gate)
        {
            _values[key] = JsonValue.Create(value);
            Save();
        }
    }

    private static JsonObject Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return [];
            return JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return []; // corrupt file — start clean rather than refusing to launch
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, _values.ToJsonString(WriteOptions));
        }
        catch (IOException)
        {
            // A setting that can't be persisted must not take the app down.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Re-reads the file; used by tests and after an external edit.</summary>
    public void Reload()
    {
        lock (_gate)
        {
            _values = Load(_path);
        }
    }
}

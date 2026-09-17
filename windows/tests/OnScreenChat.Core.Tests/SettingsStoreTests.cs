using OnScreenChat.Core.Config;

namespace OnScreenChat.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly string _path;

    public SettingsStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"onscreenchat-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Hide_from_screen_share_defaults_to_on()
    {
        // Private out of the box, as on macOS — the default that matters most.
        Assert.True(new SettingsStore(_path).HideFromScreenShare);
    }

    [Fact]
    public void Settings_persist_across_instances()
    {
        var first = new SettingsStore(_path)
        {
            HideFromScreenShare = false,
            SelectedModelId = "gemini:gemini-2.5-pro",
            DatingHeat = "spicy",
            DatingStyle = "all lowercase, no periods",
            Frame = new PanelFrame(10, 20, 380, 480),
        };
        Assert.NotNull(first.Frame);

        var second = new SettingsStore(_path);
        Assert.False(second.HideFromScreenShare);
        Assert.Equal("gemini:gemini-2.5-pro", second.SelectedModelId);
        Assert.Equal("spicy", second.DatingHeat);
        Assert.Equal("all lowercase, no periods", second.DatingStyle);
        Assert.Equal(new PanelFrame(10, 20, 380, 480), second.Frame);
    }

    [Fact]
    public void Unset_values_fall_back_rather_than_throwing()
    {
        var store = new SettingsStore(_path);
        Assert.Null(store.Frame);
        Assert.Null(store.SelectedModelId);
        Assert.Equal("auto", store.DatingHeat);
        Assert.Equal(string.Empty, store.DatingStyle);
    }

    [Fact]
    public void A_corrupt_file_is_treated_as_empty_rather_than_fatal()
    {
        File.WriteAllText(_path, "{ this is not json");

        var store = new SettingsStore(_path);
        Assert.True(store.HideFromScreenShare);

        // And it recovers: the next write produces a valid file again.
        store.DatingHeat = "warm";
        Assert.Equal("warm", new SettingsStore(_path).DatingHeat);
    }

    [Fact]
    public void A_value_of_the_wrong_type_falls_back_to_the_default()
    {
        File.WriteAllText(_path, """{"hideFromScreenShare":"yes please","panelFrame":42}""");

        var store = new SettingsStore(_path);
        Assert.True(store.HideFromScreenShare);
        Assert.Null(store.Frame);
    }

    [Fact]
    public void Clearing_the_model_id_removes_the_key()
    {
        var store = new SettingsStore(_path) { SelectedModelId = "gemini:gemini-2.5-pro" };
        store.SelectedModelId = null;

        Assert.Null(new SettingsStore(_path).SelectedModelId);
        Assert.DoesNotContain("selectedModelID", File.ReadAllText(_path));
    }

    [Fact]
    public void Uses_the_same_key_names_as_the_mac_app()
    {
        var store = new SettingsStore(_path)
        {
            HideFromScreenShare = false,
            SelectedModelId = "m",
            DatingHeat = "cool",
            DatingStyle = "s",
            Frame = PanelFrame.Default,
        };
        store.Reload();

        var json = File.ReadAllText(_path);
        Assert.Contains("panelFrame", json);
        Assert.Contains("hideFromScreenShare", json);
        Assert.Contains("selectedModelID", json);
        Assert.Contains("datingHeat", json);
        Assert.Contains("datingStyle", json);
    }

    [Fact]
    public void Creates_the_directory_when_it_is_missing()
    {
        var nested = Path.Combine(_directory, "nope", "settings.json");
        _ = new SettingsStore(nested) { DatingHeat = "warm" };
        Assert.True(File.Exists(nested));
    }
}

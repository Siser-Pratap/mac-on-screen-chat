namespace OnScreenChat.Core.Config;

/// <summary>
/// Where the app keeps its data. The Windows analogue of the Mac app's
/// <c>~/Library/Application Support/MacOnScreenChat</c>.
/// </summary>
/// <remarks>
/// Deliberately uses <see cref="Environment.SpecialFolder.LocalApplicationData"/>
/// rather than <c>ApplicationData.Current.LocalFolder</c>, which throws in an
/// unpackaged app — and this app ships unpackaged.
/// </remarks>
public static class AppPaths
{
    /// <summary>Overrides the data directory; set by tests to stay hermetic.</summary>
    public const string OverrideVariable = "ONSCREENCHAT_DATA_DIR";

    public const string FolderName = "OnScreenChat";

    public static string DataDirectory
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable(OverrideVariable);
            if (!string.IsNullOrWhiteSpace(overridden)) return overridden;

            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, FolderName);
        }
    }

    public static string DatabaseFile => Path.Combine(DataDirectory, "app.sqlite");

    public static string LogFile => Path.Combine(DataDirectory, "debug.log");

    public static string EnvFile => Path.Combine(DataDirectory, ".env");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    /// <summary>Creates the data directory if it isn't there yet; returns it.</summary>
    public static string EnsureDataDirectory()
    {
        var directory = DataDirectory;
        Directory.CreateDirectory(directory);
        return directory;
    }
}

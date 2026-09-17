using OnScreenChat.Core.Config;

namespace OnScreenChat.Core.Diagnostics;

/// <summary>
/// Lightweight file logger so we can diagnose launch/hotkey/window issues
/// regardless of how the app was started. Writes to
/// <c>%LOCALAPPDATA%\OnScreenChat\debug.log</c>.
/// </summary>
public static class Log
{
    private static readonly Lock Gate = new();

    public static void Write(string message)
    {
        var line = $"{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}  {message}{Environment.NewLine}";

        lock (Gate)
        {
            try
            {
                AppPaths.EnsureDataDirectory();
                File.AppendAllText(AppPaths.LogFile, line);
            }
            catch (IOException)
            {
                // Logging must never take the app down.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        // Also echo to stderr (visible when run from a terminal).
        Console.Error.Write(line);
    }
}

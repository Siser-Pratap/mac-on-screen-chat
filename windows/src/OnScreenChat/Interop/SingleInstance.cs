using OnScreenChat.Core.Diagnostics;

namespace OnScreenChat.Interop;

/// <summary>
/// Ensures one copy runs at a time. Windows has no equivalent of the Mac app's
/// implicit single-instance behaviour, and a second copy would fight the first
/// for the hotkey and leave two tray icons behind.
/// </summary>
/// <remarks>
/// A second launch is treated as "show me the panel": it broadcasts a registered
/// window message the running instance listens for, then exits.
/// </remarks>
internal static class SingleInstance
{
    private const string MutexName = @"Local\OnScreenChat.SingleInstance";
    private const string MessageName = "OnScreenChat.ShowPanel";

    private static Mutex? _mutex;

    /// <summary>The broadcast message that asks a running instance to show itself.</summary>
    public static uint ShowMessage { get; } = NativeMethods.RegisterWindowMessage(MessageName);

    /// <summary>
    /// True if this process owns the single-instance slot. When false the caller
    /// must exit immediately — the running copy has already been signalled.
    /// </summary>
    public static bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var created);

        if (created) return true;

        Log.Write("[instance] already running; signalling the existing window");
        NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, ShowMessage, IntPtr.Zero, IntPtr.Zero);

        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    public static void Release()
    {
        _mutex?.Dispose();
        _mutex = null;
    }
}

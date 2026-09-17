using System.Runtime.InteropServices;
using OnScreenChat.Core.Diagnostics;

namespace OnScreenChat.Interop;

/// <summary>
/// Registers a system-wide hotkey via <c>RegisterHotKey</c>. Works app-wide with
/// no accessibility or input-monitoring permission, which is what the Mac app
/// gets from Carbon's <c>RegisterEventHotKey</c>.
/// </summary>
/// <remarks>
/// The combination is <b>Ctrl+Shift+Space</b>, not Win+Shift+Space: Ctrl is the
/// idiomatic stand-in for ⌘, and Win+Space is reserved by the input-language
/// switcher.
/// </remarks>
internal sealed class GlobalHotKey : IDisposable
{
    private const int HotKeyId = 1;

    private readonly MessageWindow _window;
    private readonly Action _action;
    private bool _registered;

    public GlobalHotKey(MessageWindow window, Action action)
    {
        _window = window;
        _action = action;

        _registered = NativeMethods.RegisterHotKey(
            window.Hwnd, HotKeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            NativeMethods.VK_SPACE);

        if (!_registered)
        {
            // ERROR_HOTKEY_ALREADY_REGISTERED (1409) means another app owns it.
            Log.Write($"[hotkey] RegisterHotKey failed: {Marshal.GetLastWin32Error()}");
        }
        else
        {
            Log.Write("[hotkey] registered Ctrl+Shift+Space OK");
        }

        window.MessageReceived += OnMessage;
    }

    /// <summary>False when another app already owns the combination.</summary>
    public bool IsRegistered => _registered;

    private bool OnMessage(uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message != NativeMethods.WM_HOTKEY || wParam.ToInt64() != HotKeyId) return false;

        Log.Write("[hotkey] Ctrl+Shift+Space pressed");
        _action();
        return true;
    }

    public void Dispose()
    {
        if (!_registered) return;

        _window.MessageReceived -= OnMessage;
        NativeMethods.UnregisterHotKey(_window.Hwnd, HotKeyId);
        _registered = false;
    }
}

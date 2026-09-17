using System.Runtime.InteropServices;
using OnScreenChat.Core.Diagnostics;
using OnScreenChat.Interop;

namespace OnScreenChat.Tray;

/// <summary>
/// The tray icon — the Windows counterpart of the Mac app's menu-bar
/// <c>NSStatusItem</c>. With no taskbar button, this is the only persistent way
/// to toggle the panel, flip the screen-share setting, and quit.
/// </summary>
/// <remarks>
/// Uses <c>Shell_NotifyIcon</c> directly rather than a NuGet wrapper: the app is
/// unpackaged and already owns a message-only window, so a dependency would add
/// version surface without removing any work.
/// </remarks>
internal sealed class TrayIconController : IDisposable
{
    private const uint CallbackMessage = 0x0400 + 1; // WM_APP + 1
    private const uint IconId = 1;

    private const uint CommandToggle = 1;
    private const uint CommandHideFromShare = 2;
    private const uint CommandQuit = 3;

    private readonly MessageWindow _window;
    private readonly Action _onToggle;
    private readonly Func<bool> _onToggleHideFromShare;
    private readonly Action _onQuit;

    private IntPtr _icon;
    private bool _hiddenFromScreenShare;
    private bool _disposed;

    public TrayIconController(
        MessageWindow window,
        bool hiddenFromScreenShare,
        Action onToggle,
        Func<bool> onToggleHideFromShare,
        Action onQuit)
    {
        _window = window;
        _hiddenFromScreenShare = hiddenFromScreenShare;
        _onToggle = onToggle;
        _onToggleHideFromShare = onToggleHideFromShare;
        _onQuit = onQuit;

        _icon = LoadIcon();

        var data = new NativeMethods.NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd = window.Hwnd,
            uID = IconId,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = CallbackMessage,
            hIcon = _icon,
            szTip = "On-Screen Chat  (Ctrl+Shift+Space)",
            szInfo = string.Empty,
            szInfoTitle = string.Empty,
        };

        if (!NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref data))
        {
            Log.Write($"[tray] Shell_NotifyIcon failed: {Marshal.GetLastWin32Error()}");
        }

        window.MessageReceived += OnMessage;
        Log.Write("[tray] icon created");
    }

    /// <summary>Keeps the menu's checkmark in step when the setting changes elsewhere.</summary>
    public void SetHiddenFromScreenShare(bool hidden) => _hiddenFromScreenShare = hidden;

    private static IntPtr LoadIcon()
    {
        // Prefer the shipped .ico; fall back to the stock application icon so a
        // missing asset costs us the artwork, not the tray item itself.
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "tray.ico");
        if (File.Exists(path))
        {
            var loaded = NativeMethods.LoadImage(
                IntPtr.Zero, path, NativeMethods.IMAGE_ICON, 0, 0,
                NativeMethods.LR_LOADFROMFILE | NativeMethods.LR_DEFAULTSIZE);
            if (loaded != IntPtr.Zero) return loaded;
        }

        Log.Write("[tray] tray.ico not found; using the stock application icon");
        return NativeMethods.LoadIconW(IntPtr.Zero, NativeMethods.IDI_APPLICATION);
    }

    private bool OnMessage(uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message != CallbackMessage) return false;

        switch ((uint)lParam.ToInt64())
        {
            case NativeMethods.WM_LBUTTONUP:
                _onToggle();
                return true;

            case NativeMethods.WM_RBUTTONUP:
                ShowMenu();
                return true;

            default:
                return false;
        }
    }

    private void ShowMenu()
    {
        var menu = NativeMethods.CreatePopupMenu();
        if (menu == IntPtr.Zero) return;

        try
        {
            NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING,
                (UIntPtr)CommandToggle, "Show / Hide  (Ctrl+Shift+Space)");

            NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, UIntPtr.Zero, null);

            // Privacy toggle — the checkmark reflects whether the panel is
            // excluded from screen capture. See visibility.md.
            NativeMethods.AppendMenu(
                menu,
                NativeMethods.MF_STRING |
                    (_hiddenFromScreenShare ? NativeMethods.MF_CHECKED : NativeMethods.MF_UNCHECKED),
                (UIntPtr)CommandHideFromShare,
                "Hide from Screen Sharing");

            NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, UIntPtr.Zero, null);

            NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING,
                (UIntPtr)CommandQuit, "Quit On-Screen Chat");

            NativeMethods.GetCursorPos(out var cursor);

            // Required by the docs: without it the menu refuses to dismiss when
            // the user clicks elsewhere.
            NativeMethods.SetForegroundWindow(_window.Hwnd);

            var command = NativeMethods.TrackPopupMenu(
                menu,
                NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_RETURNCMD,
                cursor.X, cursor.Y, 0, _window.Hwnd, IntPtr.Zero);

            Invoke((uint)command);
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    private void Invoke(uint command)
    {
        switch (command)
        {
            case CommandToggle:
                _onToggle();
                break;

            case CommandHideFromShare:
                _hiddenFromScreenShare = _onToggleHideFromShare();
                break;

            case CommandQuit:
                _onQuit();
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _window.MessageReceived -= OnMessage;

        var data = new NativeMethods.NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd = _window.Hwnd,
            uID = IconId,
            szTip = string.Empty,
            szInfo = string.Empty,
            szInfoTitle = string.Empty,
        };
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref data);

        if (_icon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }
    }
}

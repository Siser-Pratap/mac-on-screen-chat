using System.Runtime.InteropServices;
using OnScreenChat.Core.Diagnostics;

namespace OnScreenChat.Interop;

/// <summary>
/// A hidden message-only window. Both the global hotkey and the tray icon need
/// an HWND with a window procedure we control, and the panel's own HWND is owned
/// by WinUI — so this exists purely to receive those messages.
/// </summary>
internal sealed class MessageWindow : IDisposable
{
    private const string ClassName = "OnScreenChatMessageWindow";

    /// <summary>Kept alive for the lifetime of the window: Win32 holds a raw pointer.</summary>
    private readonly NativeMethods.WndProc _procedure;

    private bool _disposed;

    public IntPtr Hwnd { get; }

    /// <summary>Handles a message; return true when it was consumed.</summary>
    public event Func<uint, IntPtr, IntPtr, bool>? MessageReceived;

    public MessageWindow()
    {
        _procedure = WindowProcedure;

        var instance = NativeMethods.GetModuleHandleW(IntPtr.Zero);

        var windowClass = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_procedure),
            hInstance = instance,
            lpszClassName = ClassName,
        };

        // A second registration of the same class name fails harmlessly; the
        // single-instance guard means it should not happen anyway.
        NativeMethods.RegisterClassEx(ref windowClass);

        Hwnd = NativeMethods.CreateWindowEx(
            0, ClassName, null, 0, 0, 0, 0, 0,
            NativeMethods.HWND_MESSAGE, IntPtr.Zero, instance, IntPtr.Zero);

        if (Hwnd == IntPtr.Zero)
        {
            Log.Write($"[interop] message window creation failed: {Marshal.GetLastWin32Error()}");
        }
    }

    private IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (MessageReceived?.Invoke(message, wParam, lParam) == true)
            {
                return IntPtr.Zero;
            }
        }
        catch (Exception error)
        {
            // An exception escaping into Win32 would take the process down.
            Log.Write($"[interop] handler threw: {error}");
        }

        return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (Hwnd != IntPtr.Zero) NativeMethods.DestroyWindow(Hwnd);
    }
}

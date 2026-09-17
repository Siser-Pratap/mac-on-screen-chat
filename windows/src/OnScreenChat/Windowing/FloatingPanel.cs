using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using OnScreenChat.Core.Diagnostics;
using OnScreenChat.Interop;
using Windows.System;
using WinRT.Interop;

namespace OnScreenChat.Windowing;

/// <summary>
/// A borderless, always-on-top window that floats above other apps and can still
/// receive keyboard input for the chat box. The counterpart of the Mac app's
/// <c>FloatingPanel</c> (<c>NSPanel</c>).
/// </summary>
internal sealed class FloatingPanel : Window
{
    /// <summary>Raised when the user presses Esc inside the panel.</summary>
    public event Action? CancelRequested;

    /// <summary>The window handle, for the Win32 calls the SDK doesn't wrap.</summary>
    public IntPtr Hwnd { get; }

    /// <summary>
    /// When true, the panel is excluded from screen capture and screen sharing
    /// while staying fully visible on the local display — the counterpart of
    /// <c>NSWindow.sharingType = .none</c>. See visibility.md, and §7 of
    /// windows/PLAN.md for how far this actually reaches on Windows.
    /// </summary>
    public bool IsHiddenFromScreenShare
    {
        get => NativeMethods.GetWindowDisplayAffinity(Hwnd, out var affinity)
               && affinity == NativeMethods.WDA_EXCLUDEFROMCAPTURE;
        set
        {
            var affinity = value
                ? NativeMethods.WDA_EXCLUDEFROMCAPTURE
                : NativeMethods.WDA_NONE;

            if (!NativeMethods.SetWindowDisplayAffinity(Hwnd, affinity))
            {
                // Pre-19041 systems reject WDA_EXCLUDEFROMCAPTURE outright. The
                // manifest asks for 19041+, but a failure here must be loud in
                // the log rather than silently leaving the panel capturable.
                Log.Write(
                    "[panel] SetWindowDisplayAffinity FAILED — the panel is NOT hidden from capture: " +
                    $"{Marshal.GetLastWin32Error()}");
            }
        }
    }

    public FloatingPanel()
    {
        Hwnd = WindowNative.GetWindowHandle(this);

        Title = "On-Screen Chat";

        // Closest available match for SwiftUI's .ultraThinMaterial. Acrylic is
        // switched off system-wide when transparency effects are disabled or
        // battery saver is on, in which case this falls back to a solid brush
        // and the panel simply looks opaque.
        SystemBackdrop = new DesktopAcrylicBackdrop();

        // Chromeless: the page draws its own header, as the SwiftUI view does.
        ExtendsContentIntoTitleBar = true;

        var appWindow = AppWindow;

        // No taskbar button and no Alt+Tab entry — the equivalent of the Mac
        // app's LSUIElement / .accessory activation policy.
        appWindow.IsShownInSwitchers = false;

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;

            // A border with no title bar: the border is what carries the resize
            // handles, so dropping it entirely would make the panel fixed-size.
            // The Mac panel is resizable, so this keeps parity.
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        }
        else
        {
            Log.Write("[panel] unexpected presenter; always-on-top not applied");
        }
    }

    /// <summary>
    /// Wires Esc to dismiss, once the page is in place. Content has to be set
    /// first — the accelerator lives on the root element, not the window.
    /// </summary>
    public void AttachDismissShortcut(FrameworkElement root)
    {
        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape };
        escape.Invoked += (_, args) =>
        {
            args.Handled = true;
            CancelRequested?.Invoke();
        };
        root.KeyboardAccelerators.Add(escape);
    }

    /// <summary>
    /// Ctrl+. stops a streaming reply. Esc is left alone — it hides the panel —
    /// which is exactly how the Mac app splits Esc and ⌘.
    /// </summary>
    public void AttachStopShortcut(FrameworkElement root, Action stop)
    {
        var accelerator = new KeyboardAccelerator
        {
            Key = (VirtualKey)0xBE, // VK_OEM_PERIOD
            Modifiers = VirtualKeyModifiers.Control,
        };
        accelerator.Invoked += (_, args) =>
        {
            args.Handled = true;
            stop();
        };
        root.KeyboardAccelerators.Add(accelerator);
    }
}

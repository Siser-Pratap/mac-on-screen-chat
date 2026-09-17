using Microsoft.UI.Windowing;
using OnScreenChat.Core.Config;
using OnScreenChat.Core.Diagnostics;
using OnScreenChat.Interop;
using OnScreenChat.ViewModels;
using OnScreenChat.Views;
using Windows.Graphics;

namespace OnScreenChat.Windowing;

/// <summary>
/// Owns the floating panel: show/hide/toggle, and remembers its on-screen frame.
/// Port of the Mac app's <c>PanelController</c>.
/// </summary>
internal sealed class PanelController
{
    private readonly SettingsStore _settings;
    private readonly FloatingPanel _panel;
    private readonly DragRegionManager _dragRegions;

    /// <summary>Suppresses frame saving while we are the ones moving the window.</summary>
    private bool _applyingFrame;

    /// <summary>Raised when the user closes the window; the app decides what that means.</summary>
    public event Action? CloseRequested;

    public PanelController(SettingsStore settings, ChatViewModel viewModel)
    {
        _settings = settings;

        Log.Write("[panel] init: creating FloatingPanel");
        _panel = new FloatingPanel();

        // Restore the persisted preference. Absent (first launch) it defaults to
        // true, so the panel is private out of the box.
        _panel.IsHiddenFromScreenShare = settings.HideFromScreenShare;

        var page = new ChatPage();
        page.Bind(viewModel);
        _panel.Content = page;

        _panel.AttachDismissShortcut(page);
        _panel.AttachStopShortcut(page, viewModel.Stop);
        _panel.CancelRequested += Hide;

        _dragRegions = new DragRegionManager(_panel, page);
        _dragRegions.SetInteractiveElements(page.InteractiveElements);

        RestoreFrame();

        // Persist the frame whenever the user drags or resizes the panel.
        _panel.AppWindow.Changed += OnAppWindowChanged;

        // Alt+F4 hides rather than quits: the tray icon owns the app's lifetime,
        // exactly as the Mac menu-bar item does.
        _panel.AppWindow.Closing += (_, args) =>
        {
            args.Cancel = true;
            Hide();
            CloseRequested?.Invoke();
        };

        Log.Write("[panel] init: complete");
    }

    public bool IsVisible => _panel.AppWindow.IsVisible;

    /// <summary>
    /// Whether the panel is excluded from screen capture. Persisted, so it
    /// survives a relaunch. See visibility.md.
    /// </summary>
    public bool IsHiddenFromScreenShare
    {
        get => _panel.IsHiddenFromScreenShare;
        set
        {
            _panel.IsHiddenFromScreenShare = value;
            _settings.HideFromScreenShare = value;
            Log.Write($"[panel] hideFromScreenShare={value}");
        }
    }

    /// <summary>Flips the hide-from-screen-share setting and returns the new value.</summary>
    public bool ToggleHiddenFromScreenShare()
    {
        IsHiddenFromScreenShare = !IsHiddenFromScreenShare;
        return IsHiddenFromScreenShare;
    }

    public void Toggle()
    {
        Log.Write($"[panel] toggle (visible={IsVisible})");
        if (IsVisible) Hide();
        else Show();
    }

    public void Show()
    {
        EnsureOnScreen();
        _panel.AppWindow.Show();
        _panel.Activate(); // so the text box can take keystrokes
        Log.Write($"[panel] show at {Describe(_panel.AppWindow.Position, _panel.AppWindow.Size)}");
    }

    public void Hide()
    {
        _panel.AppWindow.Hide();
        Log.Write("[panel] hide");
    }

    // MARK: - Frame persistence

    private void RestoreFrame()
    {
        var fallback = new PanelFrame(
            0, 0,
            NativeMethods.Scale(PanelFrame.Default.Width, _panel.Hwnd),
            NativeMethods.Scale(PanelFrame.Default.Height, _panel.Hwnd));

        var frame = PanelFrame.Restore(_settings.Frame, WorkAreas(), fallback);
        ApplyFrame(frame);
        Log.Write($"[panel] frame restored to {frame.Serialize()}");
    }

    /// <summary>
    /// If the saved frame ended up off every display — a monitor unplugged or
    /// rearranged since last launch — recenter so the panel can't get lost.
    /// </summary>
    private void EnsureOnScreen()
    {
        var areas = WorkAreas();
        if (areas.Count == 0) return;

        var current = CurrentFrame();
        if (current.IsOnAnyScreen(areas)) return;

        Log.Write("[panel] frame off-screen, recentering");
        ApplyFrame(current.CenteredIn(areas[0]));
    }

    private void ApplyFrame(PanelFrame frame)
    {
        _applyingFrame = true;
        try
        {
            _panel.AppWindow.MoveAndResize(
                new RectInt32(frame.X, frame.Y, frame.Width, frame.Height));
        }
        finally
        {
            _applyingFrame = false;
        }
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_applyingFrame) return;
        if (!args.DidPositionChange && !args.DidSizeChange) return;

        _settings.Frame = CurrentFrame();
    }

    private PanelFrame CurrentFrame()
    {
        var position = _panel.AppWindow.Position;
        var size = _panel.AppWindow.Size;
        return new PanelFrame(position.X, position.Y, size.Width, size.Height);
    }

    /// <summary>
    /// Every display's work area, primary first — <see cref="PanelFrame.Restore"/>
    /// centers into the first entry, and the primary display is the sane choice
    /// for a panel that has lost its home.
    /// </summary>
    private static List<PanelFrame> WorkAreas()
    {
        var primary = DisplayArea.Primary;
        var areas = new List<PanelFrame> { ToFrame(primary.WorkArea) };

        foreach (var area in DisplayArea.FindAll())
        {
            if (area.DisplayId.Value == primary.DisplayId.Value) continue;
            areas.Add(ToFrame(area.WorkArea));
        }

        return areas;
    }

    private static PanelFrame ToFrame(RectInt32 rect) =>
        new(rect.X, rect.Y, rect.Width, rect.Height);

    private static string Describe(PointInt32 position, SizeInt32 size) =>
        $"{position.X},{position.Y} {size.Width}x{size.Height}";
}

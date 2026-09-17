using Microsoft.UI.Xaml;
using OnScreenChat.Core.Config;
using OnScreenChat.Core.Data;
using OnScreenChat.Core.Diagnostics;
using OnScreenChat.Interop;
using OnScreenChat.Tray;
using OnScreenChat.ViewModels;
using OnScreenChat.Windowing;

namespace OnScreenChat;

/// <summary>
/// Entry point. The Mac app runs as an <c>.accessory</c> app with no Dock icon;
/// the equivalent here is a window kept out of the taskbar and Alt+Tab, with the
/// tray icon as the only persistent affordance.
/// </summary>
public partial class App : Application
{
    private MessageWindow? _messages;
    private PanelController? _panel;
    private GlobalHotKey? _hotKey;
    private TrayIconController? _tray;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log.Write("[app] launching");

        var settings = new SettingsStore();
        var database = AppDatabase.Shared;
        var viewModel = new ChatViewModel(database, settings);

        // Hidden window that receives WM_HOTKEY and the tray callbacks.
        _messages = new MessageWindow();
        _messages.MessageReceived += OnBroadcast;

        _panel = new PanelController(settings, viewModel);

        _hotKey = new GlobalHotKey(_messages, () => _panel.Toggle());
        if (!_hotKey.IsRegistered)
        {
            Log.Write("[app] hotkey unavailable — another app owns Ctrl+Shift+Space");
        }

        _tray = new TrayIconController(
            _messages,
            hiddenFromScreenShare: _panel.IsHiddenFromScreenShare,
            onToggle: () => _panel.Toggle(),
            onToggleHideFromShare: () => _panel.ToggleHiddenFromScreenShare(),
            onQuit: Quit);

        // Closing the window hides it; the tray owns the app's lifetime.
        _panel.CloseRequested += () => Log.Write("[app] window closed to tray");

        // Show on launch so it's immediately discoverable.
        _panel.Show();

        Log.Write("[app] launch complete");
    }

    /// <summary>A second launch asks the running copy to surface itself.</summary>
    private bool OnBroadcast(uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message != SingleInstance.ShowMessage) return false;

        Log.Write("[app] second launch detected; showing the panel");
        _panel?.Show();
        return true;
    }

    private void Quit()
    {
        Log.Write("[app] quitting");

        _tray?.Dispose();
        _hotKey?.Dispose();
        _messages?.Dispose();

        Exit();
    }
}

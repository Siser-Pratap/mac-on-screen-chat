using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using OnScreenChat.Interop;

namespace OnScreenChat;

/// <summary>
/// Replaces the XAML-generated entry point so the single-instance check can run
/// before any UI exists — a second copy would fight the first for the global
/// hotkey and leave a second tray icon behind.
/// </summary>
public static class Program
{
    [STAThread]
    private static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (!SingleInstance.TryAcquire()) return; // the running copy was signalled

        try
        {
            Application.Start(_ =>
            {
                var queue = DispatcherQueue.GetForCurrentThread();
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(queue));

                // Constructing it is the whole job: Application's constructor
                // registers it as Current and OnLaunched takes over. It cannot
                // be `_ = new App()` — `_` is the callback parameter here, so
                // that assigns to it instead of discarding.
                new App();
            });
        }
        finally
        {
            SingleInstance.Release();
        }
    }
}

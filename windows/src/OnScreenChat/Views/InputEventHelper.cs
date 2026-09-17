using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace OnScreenChat.Views;

/// <summary>
/// Reads modifier state during a key event. WinUI's <c>KeyRoutedEventArgs</c>
/// carries no modifier flags, so Shift has to be queried separately — which is
/// what decides between "send" and "newline".
/// </summary>
internal static class InputEventHelper
{
    public static bool IsShiftDown() =>
        InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(CoreVirtualKeyStates.Down);
}

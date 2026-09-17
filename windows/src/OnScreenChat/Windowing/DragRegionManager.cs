using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;

namespace OnScreenChat.Windowing;

/// <summary>
/// Makes the whole panel draggable by its background, the way
/// <c>isMovableByWindowBackground</c> does on macOS.
/// </summary>
/// <remarks>
/// Windows has no single switch for this. The approach is inverted: declare the
/// entire client area a caption (drag) region, then punch holes in it for every
/// control that needs clicks — otherwise the text box and buttons become
/// undraggable furniture that swallow no input at all.
/// <para>
/// Regions are physical pixels, so every rectangle is scaled by the rasterization
/// scale, and everything is recomputed when the window resizes or moves to a
/// display with different scaling.
/// </para>
/// </remarks>
internal sealed class DragRegionManager
{
    private readonly Window _window;
    private readonly FrameworkElement _root;
    private readonly List<FrameworkElement> _interactive = [];

    public DragRegionManager(Window window, FrameworkElement root)
    {
        _window = window;
        _root = root;

        _root.SizeChanged += (_, _) => Apply();
        _root.Loaded += (_, _) =>
        {
            if (_root.XamlRoot is { } xamlRoot)
            {
                xamlRoot.Changed += (_, _) => Apply(); // DPI or display change
            }
            Apply();
        };
    }

    /// <summary>
    /// Registers the controls that must keep receiving clicks. Call once the
    /// page's chrome exists; safe to call again as the layout changes.
    /// </summary>
    public void SetInteractiveElements(params FrameworkElement[] elements)
    {
        _interactive.Clear();
        _interactive.AddRange(elements);
        Apply();
    }

    private void Apply()
    {
        if (_root.XamlRoot is null) return;
        if (_root.ActualWidth <= 0 || _root.ActualHeight <= 0) return;

        var source = InputNonClientPointerSource.GetForWindowId(_window.AppWindow.Id);
        var scale = _root.XamlRoot.RasterizationScale;

        var caption = new RectInt32(
            0, 0,
            (int)Math.Round(_root.ActualWidth * scale),
            (int)Math.Round(_root.ActualHeight * scale));

        var passthrough = _interactive
            .Where(element => element.ActualWidth > 0 && element.ActualHeight > 0)
            .Select(element => ToPhysical(element, scale))
            .ToArray();

        source.SetRegionRects(NonClientRegionKind.Caption, [caption]);

        if (passthrough.Length > 0)
        {
            source.SetRegionRects(NonClientRegionKind.Passthrough, passthrough);
        }
        else
        {
            source.ClearRegionRects(NonClientRegionKind.Passthrough);
        }
    }

    private RectInt32 ToPhysical(FrameworkElement element, double scale)
    {
        var origin = element
            .TransformToVisual(_root)
            .TransformPoint(new Point(0, 0));

        return new RectInt32(
            (int)Math.Round(origin.X * scale),
            (int)Math.Round(origin.Y * scale),
            (int)Math.Round(element.ActualWidth * scale),
            (int)Math.Round(element.ActualHeight * scale));
    }
}

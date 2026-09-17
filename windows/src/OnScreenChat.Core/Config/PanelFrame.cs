namespace OnScreenChat.Core.Config;

/// <summary>
/// A window rectangle in physical screen pixels, and the geometry the panel
/// needs to survive a restart without getting lost.
/// </summary>
/// <remarks>
/// Deliberately a plain value type rather than a WinUI <c>RectInt32</c>, so the
/// restore-and-recover rules can be tested without a Windows machine. The
/// serialized form mirrors the Mac app's <c>NSStringFromRect</c> round-trip.
/// </remarks>
public readonly record struct PanelFrame(int X, int Y, int Width, int Height)
{
    public static PanelFrame Default => new(0, 0, 380, 480);

    public int Right => X + Width;

    public int Bottom => Y + Height;

    public string Serialize() => $"{X},{Y},{Width},{Height}";

    public static PanelFrame? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var parts = text.Split(',');
        if (parts.Length != 4) return null;

        var values = new int[4];
        for (var i = 0; i < 4; i++)
        {
            if (!int.TryParse(parts[i].Trim(), out values[i])) return null;
        }

        // A zero-or-negative size would restore an invisible window.
        if (values[2] <= 0 || values[3] <= 0) return null;

        return new PanelFrame(values[0], values[1], values[2], values[3]);
    }

    public bool Intersects(PanelFrame other) =>
        X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;

    /// <summary>
    /// True when the frame overlaps at least one display's work area. A saved
    /// frame can end up nowhere after a monitor is unplugged or rearranged.
    /// </summary>
    public bool IsOnAnyScreen(IReadOnlyList<PanelFrame> workAreas) =>
        workAreas.Any(Intersects);

    /// <summary>Same size, centered in the given work area.</summary>
    public PanelFrame CenteredIn(PanelFrame workArea) => this with
    {
        X = workArea.X + ((workArea.Width - Width) / 2),
        Y = workArea.Y + ((workArea.Height - Height) / 2),
    };

    /// <summary>
    /// The frame to actually show: the saved one when it is still reachable,
    /// otherwise the same size recentered on the primary display.
    /// </summary>
    public static PanelFrame Restore(
        PanelFrame? saved, IReadOnlyList<PanelFrame> workAreas, PanelFrame? fallback = null)
    {
        var frame = saved ?? fallback ?? Default;

        if (workAreas.Count == 0) return frame;
        if (saved is not null && frame.IsOnAnyScreen(workAreas)) return frame;

        return frame.CenteredIn(workAreas[0]);
    }
}

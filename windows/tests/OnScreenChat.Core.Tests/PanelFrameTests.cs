using OnScreenChat.Core.Config;

namespace OnScreenChat.Core.Tests;

public class PanelFrameTests
{
    private static readonly PanelFrame Primary = new(0, 0, 1920, 1040);
    private static readonly PanelFrame Secondary = new(1920, 0, 1920, 1040);

    [Fact]
    public void Round_trips_through_its_serialized_form()
    {
        var frame = new PanelFrame(100, 200, 380, 480);
        Assert.Equal(frame, PanelFrame.TryParse(frame.Serialize()));
    }

    [Fact]
    public void Negative_coordinates_survive_a_round_trip()
    {
        // A monitor left of the primary display has negative X — a real layout.
        var frame = new PanelFrame(-1920, -200, 380, 480);
        Assert.Equal(frame, PanelFrame.TryParse(frame.Serialize()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("1,2,3")]
    [InlineData("1,2,3,4,5")]
    [InlineData("a,b,c,d")]
    [InlineData("0,0,0,480")]   // zero width would restore an invisible window
    [InlineData("0,0,380,-1")]
    public void Unusable_input_parses_as_null(string? text)
    {
        Assert.Null(PanelFrame.TryParse(text));
    }

    [Fact]
    public void Detects_overlap_with_a_display()
    {
        Assert.True(new PanelFrame(100, 100, 380, 480).IsOnAnyScreen([Primary]));
    }

    [Fact]
    public void A_frame_on_the_second_monitor_counts_as_on_screen()
    {
        Assert.True(new PanelFrame(2000, 100, 380, 480).IsOnAnyScreen([Primary, Secondary]));
    }

    [Fact]
    public void A_frame_where_that_monitor_used_to_be_does_not()
    {
        // The exact failure the recovery exists for: second display unplugged.
        Assert.False(new PanelFrame(2000, 100, 380, 480).IsOnAnyScreen([Primary]));
    }

    [Fact]
    public void Touching_edges_do_not_count_as_overlap()
    {
        Assert.False(new PanelFrame(1920, 0, 380, 480).IsOnAnyScreen([Primary]));
    }

    [Fact]
    public void Partially_visible_still_counts_so_a_dragged_panel_stays_put()
    {
        Assert.True(new PanelFrame(1900, 100, 380, 480).IsOnAnyScreen([Primary]));
    }

    [Fact]
    public void Centering_keeps_the_size_and_uses_the_work_area_origin()
    {
        var centered = new PanelFrame(0, 0, 380, 480).CenteredIn(new PanelFrame(100, 50, 1920, 1040));

        Assert.Equal(380, centered.Width);
        Assert.Equal(480, centered.Height);
        Assert.Equal(100 + ((1920 - 380) / 2), centered.X);
        Assert.Equal(50 + ((1040 - 480) / 2), centered.Y);
    }

    [Fact]
    public void Restore_keeps_a_frame_that_is_still_reachable()
    {
        var saved = new PanelFrame(100, 100, 380, 480);
        Assert.Equal(saved, PanelFrame.Restore(saved, [Primary]));
    }

    [Fact]
    public void Restore_recenters_a_frame_that_is_now_off_screen()
    {
        var saved = new PanelFrame(5000, 100, 380, 480);
        var restored = PanelFrame.Restore(saved, [Primary]);

        Assert.NotEqual(saved, restored);
        Assert.True(restored.IsOnAnyScreen([Primary]));
        Assert.Equal(saved.Width, restored.Width); // the size the user chose is kept
    }

    [Fact]
    public void Restore_centers_the_default_on_first_launch()
    {
        var restored = PanelFrame.Restore(null, [Primary]);

        Assert.Equal(PanelFrame.Default.Width, restored.Width);
        Assert.True(restored.IsOnAnyScreen([Primary]));
    }

    [Fact]
    public void Restore_falls_back_to_the_given_size_when_nothing_was_saved()
    {
        var fallback = new PanelFrame(0, 0, 500, 600);
        Assert.Equal(500, PanelFrame.Restore(null, [Primary], fallback).Width);
    }

    [Fact]
    public void Restore_with_no_displays_reported_leaves_the_frame_alone()
    {
        // Rather than divide by an empty display list and land at 0,0.
        var saved = new PanelFrame(100, 100, 380, 480);
        Assert.Equal(saved, PanelFrame.Restore(saved, []));
    }
}

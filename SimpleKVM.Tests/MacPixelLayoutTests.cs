using SimpleKVM.Displays;
using SimpleKVM.Displays.mac;
using SimpleKVM.Platform;

namespace SimpleKVM.Tests;

// macOS lays displays out in points, and a scaled ("looks like") display has fewer points than
// pixels. Monitor ids come from the layout in pixels, so changing a display's scaling, or the
// scaling of the display beside it, never changes a monitor's id; rules saved under the ids
// versions before 2.5.0 made from points are moved over.
public class MacPixelLayoutTests
{
    static MacDisplay Display(uint id, double x, double y, double width, double height, int nativeWidth, int nativeHeight, bool builtIn = false) =>
        new(id, builtIn, x, y, width, height, nativeWidth, nativeHeight);

    static ScreenRect Rect(int left, int top, int right, int bottom) => new(left, top, right, bottom);

    static IEnumerable<string> Ids(IEnumerable<MacScreen> screens) => screens.Select(s => MacPixelLayout.Id(s.Pixels));

    [Fact]
    public void A_display_at_its_native_resolution_keeps_its_bounds_and_its_id()
    {
        // A 1080p monitor shown at 1920x1080: nothing to convert, and its id is the one it has always had
        var screens = MacPixelLayout.Build([Display(1, 0, 0, 1920, 1080, 1920, 1080)]);

        Assert.Equal(Rect(0, 0, 1920, 1080), screens[0].Pixels);
        Assert.Equal(screens[0].Points, screens[0].Pixels);
        Assert.Equal("EC89B75820FF1E3AA41E0413A85D7C94", MacPixelLayout.Id(screens[0].Pixels));
        Assert.Empty(MacPixelLayout.EarlierIds(screens));
    }

    [Fact]
    public void A_scaled_4k_monitor_is_laid_out_at_its_native_pixels()
    {
        // A 4K monitor that looks like 1920x1080, with a 1440p monitor at its native resolution to its right
        var screens = MacPixelLayout.Build([Display(1, 0, 0, 1920, 1080, 3840, 2160), Display(2, 1920, 0, 2560, 1440, 2560, 1440)]);

        Assert.Equal(Rect(0, 0, 3840, 2160), screens[0].Pixels);
        Assert.Equal(Rect(3840, 0, 6400, 1440), screens[1].Pixels);
    }

    [Fact]
    public void Changing_a_monitors_scaling_changes_no_id()
    {
        // The same two monitors with the 4K looking like 1920x1080, then like 2560x1440
        MacDisplay[] lookLike1080 = [Display(1, 0, 0, 1920, 1080, 3840, 2160), Display(2, 1920, 0, 2560, 1440, 2560, 1440)];
        MacDisplay[] lookLike1440 = [Display(1, 0, 0, 2560, 1440, 3840, 2160), Display(2, 2560, 0, 2560, 1440, 2560, 1440)];

        var before = MacPixelLayout.Build(lookLike1080);
        var after = MacPixelLayout.Build(lookLike1440);

        Assert.Equal(Ids(before), Ids(after));

        // What went wrong before 2.5.0: in points, both monitors' ids changed
        Assert.NotEqual(before[0].Points, after[0].Points);
        Assert.NotEqual(before[1].Points, after[1].Points);
    }

    [Fact]
    public void Changing_a_built_in_screens_scaling_leaves_the_monitor_beside_it_alone()
    {
        // A MacBook's 3024x1964 Retina screen with a 1440p monitor to its right. Choosing "More
        // Space" for the laptop moves the monitor in points, from 1512 to 1800, but not in pixels
        var defaultScaling = MacPixelLayout.Build([Display(1, 0, 0, 1512, 982, 3024, 1964, builtIn: true), Display(2, 1512, 0, 2560, 1440, 2560, 1440)]);
        var moreSpace = MacPixelLayout.Build([Display(1, 0, 0, 1800, 1169, 3024, 1964, builtIn: true), Display(2, 1800, 0, 2560, 1440, 2560, 1440)]);

        Assert.Equal(Rect(3024, 0, 5584, 1440), defaultScaling[1].Pixels);
        Assert.Equal(defaultScaling[1].Pixels, moreSpace[1].Pixels);
    }

    [Fact]
    public void A_scaled_monitor_gets_the_id_windows_gives_it()
    {
        // Windows reports a 4K monitor at its native resolution as 3840x2160 pixels, whatever its scale
        var screens = MacPixelLayout.Build([Display(1, 0, 0, 2560, 1440, 3840, 2160)]);

        Assert.Equal(MonitorIdentity.FromBounds(0, 0, 3840, 2160), MacPixelLayout.Id(screens[0].Pixels));
    }

    [Fact]
    public void A_rotated_monitor_is_laid_out_turned()
    {
        // A 1440p monitor turned to portrait: its display modes still say 2560x1440
        var screens = MacPixelLayout.Build([Display(1, 0, 0, 1440, 2560, 2560, 1440)]);

        Assert.Equal(Rect(0, 0, 1440, 2560), screens[0].Pixels);
    }

    [Fact]
    public void An_unknown_native_resolution_falls_back_to_points()
    {
        var screens = MacPixelLayout.Build([Display(1, 0, 0, 1920, 1080, 0, 0)]);

        Assert.Equal(screens[0].Points, screens[0].Pixels);
    }

    [Fact]
    public void Earlier_ids_are_given_for_external_displays_whose_id_changed()
    {
        var screens = MacPixelLayout.Build([
            Display(1, 0, 0, 1512, 982, 3024, 1964, builtIn: true),     // the laptop's own screen: never a rule's target
            Display(2, 1512, 0, 1920, 1080, 3840, 2160),                // a 4K monitor that looks like 1920x1080
            Display(3, 0, 982, 1920, 1080, 1920, 1080),                 // a 1080p monitor below the laptop, unscaled
        ]);

        var earlier = MacPixelLayout.EarlierIds(screens);

        // The 4K monitor's id changed. The 1080p monitor's changed too, because the laptop above it
        // is scaled: in pixels it sits 1964 down, not 982.
        Assert.Equal(new Dictionary<string, string>
        {
            [MonitorIdentity.FromBounds(1512, 0, 3432, 1080)] = MonitorIdentity.FromBounds(3024, 0, 6864, 2160),
            [MonitorIdentity.FromBounds(0, 982, 1920, 2062)] = MonitorIdentity.FromBounds(0, 1964, 1920, 3044),
        }, earlier);
    }

    [Fact]
    public void An_earlier_id_that_is_also_a_current_id_is_left_alone()
    {
        // A rule saved under this id already matches a monitor connected now, so moving it would be wrong
        MacScreen[] screens =
        [
            new(1, false, Pixels: Rect(0, 0, 200, 200), Points: Rect(0, 0, 100, 100)),
            new(2, false, Pixels: Rect(0, 0, 100, 100), Points: Rect(500, 0, 600, 100)),
        ];

        var earlier = MacPixelLayout.EarlierIds(screens);

        Assert.False(earlier.ContainsKey(MonitorIdentity.FromBounds(0, 0, 100, 100)));
        Assert.Equal(MonitorIdentity.FromBounds(0, 0, 100, 100), earlier[MonitorIdentity.FromBounds(500, 0, 600, 100)]);
    }

    [Fact]
    public void Mirrored_displays_share_an_earlier_id_and_neither_is_moved()
    {
        // Two monitors mirroring each other, one of them a scaled 4K: a rule under their shared
        // earlier id can't be told apart, so it is left as it was
        var screens = MacPixelLayout.Build([Display(1, 0, 0, 1920, 1080, 3840, 2160), Display(2, 0, 0, 1920, 1080, 1920, 1080)]);

        Assert.Empty(MacPixelLayout.EarlierIds(screens));
    }
}

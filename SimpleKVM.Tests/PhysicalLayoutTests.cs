using SimpleKVM.Displays;
using SimpleKVM.Displays.linux;
using Output = SimpleKVM.Displays.linux.PhysicalLayout.Output;
using Rect = SimpleKVM.Displays.linux.PhysicalLayout.Rect;

namespace SimpleKVM.Tests;

// Linux compositors report a logical (scaled) layout. Monitor ids must come from physical pixels,
// or changing the desktop scale orphans every rule that targets a monitor.
public class PhysicalLayoutTests
{
    public static TheoryData<string, Dictionary<string, Output>, Dictionary<string, Rect>> Layouts => new()
    {
        {
            "two 1440p side by side at 100%",
            new() { ["A"] = new(0, 0, 2560, 1440, 2560, 1440), ["B"] = new(2560, 0, 2560, 1440, 2560, 1440) },
            new() { ["A"] = new(0, 0, 2560, 1440), ["B"] = new(2560, 0, 2560, 1440) }
        },
        {
            "two 1440p side by side at 125%",
            new() { ["A"] = new(0, 0, 2048, 1152, 2560, 1440), ["B"] = new(2048, 0, 2048, 1152, 2560, 1440) },
            new() { ["A"] = new(0, 0, 2560, 1440), ["B"] = new(2560, 0, 2560, 1440) }
        },
        {
            "mixed scales: 1080p at 100% left of 4K at 200%",
            new() { ["A"] = new(0, 0, 1920, 1080, 1920, 1080), ["B"] = new(1920, 0, 1920, 1080, 3840, 2160) },
            new() { ["A"] = new(0, 0, 1920, 1080), ["B"] = new(1920, 0, 3840, 2160) }
        },
        {
            "monitor left of the origin at 150%",
            new() { ["A"] = new(0, 0, 1920, 1080, 1920, 1080), ["B"] = new(-1280, 0, 1280, 720, 1920, 1080) },
            new() { ["A"] = new(0, 0, 1920, 1080), ["B"] = new(-1920, 0, 1920, 1080) }
        },
        {
            "stacked vertically at 125%",
            new() { ["A"] = new(0, 0, 2048, 1152, 2560, 1440), ["B"] = new(0, 1152, 2048, 1152, 2560, 1440) },
            new() { ["A"] = new(0, 0, 2560, 1440), ["B"] = new(0, 1440, 2560, 1440) }
        },
        {
            "three across at 125%",
            new() { ["A"] = new(0, 0, 2048, 1152, 2560, 1440), ["B"] = new(2048, 0, 2048, 1152, 2560, 1440), ["C"] = new(4096, 0, 1536, 864, 1920, 1080) },
            new() { ["A"] = new(0, 0, 2560, 1440), ["B"] = new(2560, 0, 2560, 1440), ["C"] = new(5120, 0, 1920, 1080) }
        },
        {
            "gap between monitors falls back to scaled offset",
            new() { ["A"] = new(0, 0, 2048, 1152, 2560, 1440), ["B"] = new(4000, 0, 2048, 1152, 2560, 1440) },
            new() { ["A"] = new(0, 0, 2560, 1440), ["B"] = new(5000, 0, 2560, 1440) }
        },
    };

    [Theory]
    [MemberData(nameof(Layouts))]
    public void ToPhysical_rebuilds_pixel_layout(string name, Dictionary<string, Output> logical, Dictionary<string, Rect> expected)
    {
        Assert.NotNull(name);
        Assert.Equal(expected, PhysicalLayout.ToPhysical(logical));
    }

    [Fact]
    public void Ids_do_not_change_with_desktop_scale()
    {
        // The reported rig: two 2560x1440 monitors whose rules were created at 100%, then KDE set to 125%.
        var at125 = PhysicalLayout.ToPhysical(new Dictionary<string, Output>
        {
            ["DP-2"] = new(0, 0, 2048, 1152, 2560, 1440),
            ["DP-1"] = new(2048, 0, 2048, 1152, 2560, 1440),
        });

        string Id(Rect r) => MonitorIdentity.FromBounds(r.X, r.Y, r.X + r.Width, r.Y + r.Height);
        Assert.Equal("8E34800754286219FDAB0601FDF57760", Id(at125["DP-2"]));
        Assert.Equal("60C283E44D7F76162144AAC68741F48C", Id(at125["DP-1"]));
    }
}

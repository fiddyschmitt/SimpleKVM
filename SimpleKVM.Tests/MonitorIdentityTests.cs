using SimpleKVM.Displays;

namespace SimpleKVM.Tests;

// MonitorUniqueId is an MD5 over screen bounds and is stored in rules.json. The format is
// frozen: if these golden values ever change, every existing user's rules stop matching their
// monitors. The expected hashes were captured from real hardware (the Acer on the Mac mini and
// the two Philips monitors on the Windows box) via `--list-monitors`.
public class MonitorIdentityTests
{
    [Theory]
    [InlineData(0, 0, 1920, 1080, "EC89B75820FF1E3AA41E0413A85D7C94")]
    [InlineData(2560, 0, 5120, 1440, "60C283E44D7F76162144AAC68741F48C")]
    [InlineData(0, 0, 2560, 1440, "8E34800754286219FDAB0601FDF57760")]
    public void FromBounds_matches_hardware_verified_golden_ids(int left, int top, int right, int bottom, string expected)
    {
        Assert.Equal(expected, MonitorIdentity.FromBounds(left, top, right, bottom));
    }

    [Fact]
    public void FromBounds_is_the_md5_of_comma_joined_bounds()
    {
        // Documents the exact hashed string, so the "l,t,r,b" format can't drift silently.
        Assert.Equal("0,0,1920,1080".CreateMD5(), MonitorIdentity.FromBounds(0, 0, 1920, 1080));
    }

    [Fact]
    public void FromBounds_is_deterministic_and_position_sensitive()
    {
        Assert.Equal(MonitorIdentity.FromBounds(0, 0, 1920, 1080), MonitorIdentity.FromBounds(0, 0, 1920, 1080));
        Assert.NotEqual(MonitorIdentity.FromBounds(0, 0, 1920, 1080), MonitorIdentity.FromBounds(1920, 0, 3840, 1080));
    }

    [Fact]
    public void CreateMD5_is_32_uppercase_hex_characters()
    {
        var hash = "anything".CreateMD5();
        Assert.Equal(32, hash.Length);
        Assert.Matches("^[0-9A-F]{32}$", hash);
    }
}

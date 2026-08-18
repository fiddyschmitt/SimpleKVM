using Newtonsoft.Json;
using SimpleKVM.Configuration;

namespace SimpleKVM.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_are_off()
    {
        var settings = new AppSettings();

        // Follow-source-changes defaulting off is a deliberate behaviour choice; lock it.
        Assert.False(settings.ForceInputChange);
        Assert.False(settings.FollowSourceChanges);
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var original = new AppSettings { ForceInputChange = true, FollowSourceChanges = true };

        var loaded = JsonConvert.DeserializeObject<AppSettings>(JsonConvert.SerializeObject(original))!;

        Assert.True(loaded.ForceInputChange);
        Assert.True(loaded.FollowSourceChanges);
    }
}

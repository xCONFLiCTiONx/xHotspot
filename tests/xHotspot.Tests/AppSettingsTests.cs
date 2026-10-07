using Xunit;
using xHotspot.Core.Models;

namespace xHotspot.Tests;

public class AppSettingsTests
{
    [Fact]
    public void TurnOffHotspotOnSleep_DefaultsToTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.TurnOffHotspotOnSleep);
    }
}

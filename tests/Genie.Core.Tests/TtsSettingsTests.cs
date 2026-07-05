using System;
using Genie.Core.Config;
using Genie.Core.Runtime;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Public issue #89 — TTS rate + volume settings. <c>ttsrate</c> is a speaking
/// speed multiplier (0.5–3.0, 1 = the voice's natural pace), <c>ttsvolume</c> a
/// 0–100 percent attenuation. Both persist in settings.cfg; garbage input must
/// leave the current value untouched (unlike a clamp-to-floor, which would turn
/// a typo into half-speed speech).
/// </summary>
public class TtsSettingsTests
{
    private static GenieConfig NewConfig() =>
        new(new LocalDirectoryService("Genie5Test", AppContext.BaseDirectory));

    [Theory]
    [InlineData("1", 1.0)]
    [InlineData("1.25", 1.25)]
    [InlineData("0.5", 0.5)]
    [InlineData("3", 3.0)]
    [InlineData("0.1", 0.5)]     // below range clamps up
    [InlineData("10", 3.0)]      // above range clamps down
    public void TtsRate_ParsesAndClamps(string input, double expected)
    {
        var cfg = NewConfig();
        cfg.SetSetting("ttsrate", input, showException: false);
        Assert.Equal(expected, cfg.TtsRate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("0")]            // rate 0 is meaningless — not a mute knob
    public void TtsRate_GarbageLeavesValueUntouched(string input)
    {
        var cfg = NewConfig();
        cfg.SetSetting("ttsrate", "1.5", showException: false);
        cfg.SetSetting("ttsrate", input, showException: false);
        Assert.Equal(1.5, cfg.TtsRate);
    }

    [Fact]
    public void TtsRate_RoundTripsInvariant()
    {
        var cfg = NewConfig();
        cfg.SetSetting("ttsrate", "1.25", showException: false);
        // Written with a dot regardless of OS culture, so settings.cfg reloads
        // to the same value on a comma-decimal locale.
        Assert.Equal("1.25", cfg.GetSetting("ttsrate"));
    }

    [Theory]
    [InlineData("100", 100)]
    [InlineData("50", 50)]
    [InlineData("0", 0)]         // 0 = silent (the volume mute)
    [InlineData("150", 100)]     // above range clamps down — attenuation only
    public void TtsVolume_ParsesAndClamps(string input, int expected)
    {
        var cfg = NewConfig();
        cfg.SetSetting("ttsvolume", input, showException: false);
        Assert.Equal(expected, cfg.TtsVolume);
        Assert.Equal(expected.ToString(), cfg.GetSetting("ttsvolume"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    public void TtsVolume_GarbageLeavesValueUntouched(string input)
    {
        var cfg = NewConfig();
        cfg.SetSetting("ttsvolume", "75", showException: false);
        cfg.SetSetting("ttsvolume", input, showException: false);
        Assert.Equal(75, cfg.TtsVolume);
    }

    [Fact]
    public void Defaults_AreNaturalRateFullVolume()
    {
        var cfg = NewConfig();
        Assert.Equal(1.0, cfg.TtsRate);
        Assert.Equal(100, cfg.TtsVolume);
    }
}

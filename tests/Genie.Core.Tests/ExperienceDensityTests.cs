using System;
using Genie.Core.Config;
using Genie.Core.Extensions.Builtin;
using Genie.Core.Runtime;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Public issue #125 — Experience-window line density (Genie 4 EXPTracker parity).
/// One <c>#config experiencedensity</c> value (0–2) drives the line format the
/// "Experience" dock panel renders. 0 = Full (rank, %, learning word, count);
/// 1 = drop the (n/34) count; 2 = numbers only. Levels 3–4 (short names) reserved.
/// </summary>
public class ExperienceDensityTests
{
    // "Small Edged", rank 142, 71%, mindstate 13 ("examining" in the canonical table).
    [Theory]
    [InlineData(0, "Small Edged        142 71%  examining (13/34)")]
    [InlineData(1, "Small Edged        142 71%  examining")]
    [InlineData(2, "Small Edged        142 71%")]
    [InlineData(3, "Sm Edged     142 71%")]
    [InlineData(4, "Sm Edged     142")]
    public void FormatLine_RendersExpectedFormatPerDensity(int density, string expected)
    {
        var line = ExperienceExtension.FormatLine("Small Edged", 142, 71, 13, density);
        Assert.Equal(expected, line);
    }

    [Theory]
    [InlineData("Small Edged", "Sm Edged")]        // multi-word: clip all but last
    [InlineData("Twohanded Blunt", "Tw Blunt")]
    [InlineData("Astrology", "Astrology")]          // single word: unchanged
    [InlineData("Scholarship", "Scholarship")]
    public void ShortName_ClipsLeadingWordsOnly(string input, string expected)
    {
        Assert.Equal(expected, ExperienceExtension.ShortName(input));
    }

    [Fact]
    public void FormatLine_FullAndNoCount_KeepAlignedColumns()
    {
        // Name column (18) + rank (3) + percent (2) must line up across densities so
        // a switch doesn't ragged the list.
        var full = ExperienceExtension.FormatLine("Astrology", 432, 15, 1, 0);
        var nums = ExperienceExtension.FormatLine("Astrology", 432, 15, 1, 2);
        Assert.StartsWith("Astrology          432 15%", full);
        Assert.Equal("Astrology          432 15%", nums);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData("4", 4)]      // top live level
    [InlineData("7", 4)]      // above-range clamps down to Brief
    [InlineData("-1", 0)]     // below-range clamps up to Full
    [InlineData("", 0)]       // unset / unparseable falls back to Full
    [InlineData("abc", 0)]
    public void Config_ExperienceDensity_ClampsAndRoundTrips(string input, int expected)
    {
        var cfg = new GenieConfig(new LocalDirectoryService("Genie5Test", AppContext.BaseDirectory));
        cfg.SetSetting("experiencedensity", input, showException: false);
        Assert.Equal(expected, cfg.ExperienceDensity);
        Assert.Equal(expected.ToString(), cfg.GetSetting("experiencedensity"));
    }
}

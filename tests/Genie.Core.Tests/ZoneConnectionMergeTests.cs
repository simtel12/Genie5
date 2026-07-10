using System.Linq;
using Genie.Core.Mapper;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// <see cref="ZoneConnectionMerge"/> — the fix for the seeded-baseline shadow:
/// Genie seeds a placeholder (all-<c>TODO</c>) <c>ZoneConnections.xml</c> on first
/// launch, and the old "authored wins if any exist" rule let those placeholders
/// suppress every working derived cross-zone link. Merge must keep derived links
/// live, let real authored entries augment/override, and never let placeholder
/// rows starve the pathfinder.
/// </summary>
public class ZoneConnectionMergeTests
{
    private static ZoneConnection Conn(string fromZone, string fromRoom, string toZone,
                                       string toRoom, string verb = "go") =>
        new() { FromZone = fromZone, FromRoom = fromRoom, ToZone = toZone, ToRoom = toRoom, Verb = verb };

    [Fact]
    public void Seeded_all_TODO_baseline_does_not_suppress_derived_links()
    {
        // The actual regression: a freshly-seeded baseline is all TODO room ids.
        var derived = new[]
        {
            Conn("Map01_Crossing", "127", "Map04_Riverhaven", "88"),
            Conn("Map04_Riverhaven", "88", "Map01_Crossing", "127"),
        };
        var seededBaseline = new[]
        {
            Conn("Map01_Crossing", "TODO", "Map04_Riverhaven", "TODO", "board wagon"),
            Conn("Map04_Riverhaven", "TODO", "Map01_Crossing", "TODO", "board wagon"),
        };

        var merged = ZoneConnectionMerge.Merge(derived, seededBaseline);

        // Every working derived link survives...
        Assert.Contains(merged, c => c.FromRoom == "127" && c.ToRoom == "88" && c.Verb == "go");
        Assert.Contains(merged, c => c.FromRoom == "88" && c.ToRoom == "127" && c.Verb == "go");
        // ...and the placeholder rows ride along harmlessly (distinct endpoints).
        Assert.Equal(4, merged.Count);
    }

    [Fact]
    public void Real_authored_route_augments_the_derived_graph()
    {
        var derived  = new[] { Conn("Map01_Crossing", "127", "Map04_Riverhaven", "88") };
        var authored = new[] { Conn("Map04_Riverhaven", "90", "Map35_Throne_City", "5", "board boat") };

        var merged = ZoneConnectionMerge.Merge(derived, authored);

        Assert.Equal(2, merged.Count);
        Assert.Contains(merged, c => c.ToZone == "Map35_Throne_City" && c.Verb == "board boat");
        Assert.Contains(merged, c => c.FromRoom == "127" && c.ToRoom == "88"); // derived kept
    }

    [Fact]
    public void Authored_overrides_the_derived_link_on_matching_endpoints()
    {
        // Same from/to as a derived border link, but curated with real verb + wait.
        var derived  = new[] { Conn("Map01_Crossing", "127", "Map04_Riverhaven", "88", "north") };
        var authored = new[]
        {
            new ZoneConnection { FromZone = "Map01_Crossing", FromRoom = "127",
                                 ToZone = "Map04_Riverhaven", ToRoom = "88",
                                 Verb = "board wagon", WaitMin = 120, WaitMax = 300 },
        };

        var merged = ZoneConnectionMerge.Merge(derived, authored);

        var only = Assert.Single(merged);          // overridden in place, not appended
        Assert.Equal("board wagon", only.Verb);
        Assert.Equal(120, only.WaitMin);
    }

    [Fact]
    public void Endpoint_match_is_case_insensitive()
    {
        var derived  = new[] { Conn("Map01_Crossing", "127", "Map04_Riverhaven", "88", "north") };
        var authored = new[] { Conn("map01_crossing", "127", "MAP04_riverhaven", "88", "board wagon") };

        var merged = ZoneConnectionMerge.Merge(derived, authored);

        var only = Assert.Single(merged);
        Assert.Equal("board wagon", only.Verb);
    }

    [Fact]
    public void Empty_authored_returns_derived_unchanged()
    {
        var derived = new[] { Conn("A", "1", "B", "2"), Conn("B", "2", "A", "1") };
        var merged  = ZoneConnectionMerge.Merge(derived, System.Array.Empty<ZoneConnection>());
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void Empty_derived_returns_authored()
    {
        var authored = new[] { Conn("A", "1", "B", "2", "board boat") };
        var merged   = ZoneConnectionMerge.Merge(System.Array.Empty<ZoneConnection>(), authored);
        var only     = Assert.Single(merged);
        Assert.Equal("board boat", only.Verb);
    }
}

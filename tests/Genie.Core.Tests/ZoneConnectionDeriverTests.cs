using System.Linq;
using Genie.Core.Mapper;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Cross-zone travel (Phase 2) — community maps ship no ZoneConnections.xml; they
/// link zones at border rooms (a note naming the neighbour zone + a destination-less
/// directional arc). <see cref="ZoneConnectionDeriver"/> turns those into the
/// cross-zone edges the multi-zone pathfinder needs.
/// </summary>
public class ZoneConnectionDeriverTests
{
    // A border room: a cross-zone note + a destination-less directional arc (the way
    // out) + an ordinary in-zone arc (has a destination).
    private static MapNode Border(int id, string title, string note, Direction dir, string move)
    {
        var n = new MapNode { Id = id, Title = title, Notes = note };
        n.Exits.Add(new MapExit { Direction = dir, MoveCommand = move, DestinationId = null });
        n.Exits.Add(new MapExit { Direction = Direction.None, MoveCommand = "go gate", DestinationId = 99 });
        return n;
    }

    private static MapZone Zone(params MapNode[] nodes)
    {
        var z = new MapZone();
        foreach (var n in nodes) z.Nodes[n.Id] = n;
        return z;
    }

    [Fact]
    public void Derive_ReciprocalBorderRooms_ProducesBidirectionalEdges()
    {
        var conns = ZoneConnectionDeriver.Derive(new[]
        {
            ("Map1_Crossing", Zone(
                Border(170, "Eastern Tier, Outside Gate", "Map8_East_Gate.xml|E Gate|East", Direction.East, "east"))),
            ("Map8_East_Gate", Zone(
                Border(43, "The Crossing, Eastern Gate", "Map1_Crossing.xml|Crossing", Direction.West, "west"))),
        });

        Assert.Equal(2, conns.Count);

        var out1 = conns.Single(c => c.FromZone == "Map1_Crossing");
        Assert.Equal("170", out1.FromRoom);
        Assert.Equal("Map8_East_Gate", out1.ToZone);
        Assert.Equal("43", out1.ToRoom);
        Assert.Equal("east", out1.Verb);

        var back = conns.Single(c => c.FromZone == "Map8_East_Gate");
        Assert.Equal("43", back.FromRoom);
        Assert.Equal("Map1_Crossing", back.ToZone);
        Assert.Equal("170", back.ToRoom);
        Assert.Equal("west", back.Verb);
    }

    [Fact]
    public void Derive_MultipleReciprocals_DisambiguatesByReciprocalDirection()
    {
        var conns = ZoneConnectionDeriver.Derive(new[]
        {
            ("Map1_Crossing", Zone(
                Border(170, "Outside Gate", "Map8_East_Gate.xml|East", Direction.East, "east"))),
            ("Map8_East_Gate", Zone(
                // battlements note back too, but via Up — the gate (West) is the real pair.
                Border(124, "East Battlements", "Map1_Crossing.xml", Direction.Up, "up"),
                Border(43,  "Eastern Gate",     "Map1_Crossing.xml", Direction.West, "west"))),
        });

        var out1 = conns.Single(c => c.FromZone == "Map1_Crossing");
        Assert.Equal("43", out1.ToRoom);   // West room chosen, not the Up battlements
    }

    [Fact]
    public void Derive_OneSidedNote_ProducesNoEdge()
    {
        // Map1 notes Map8, but Map8 has no room noting back → not a confirmed link.
        var conns = ZoneConnectionDeriver.Derive(new[]
        {
            ("Map1_Crossing",   Zone(Border(170, "Gate", "Map8_East_Gate.xml|East", Direction.East, "east"))),
            ("Map8_East_Gate",  Zone(new MapNode { Id = 43, Title = "Plain room" })),
        });
        Assert.Empty(conns);
    }

    [Fact]
    public void Derive_TargetZoneNotLoaded_ProducesNoEdge()
    {
        var conns = ZoneConnectionDeriver.Derive(new[]
        {
            ("Map1_Crossing", Zone(Border(170, "Gate", "Map999_Missing.xml|X", Direction.East, "east"))),
        });
        Assert.Empty(conns);
    }
}

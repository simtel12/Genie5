using Genie.Core.Mapper;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Public #123/#122 travel work (Phase 2) — cross-zone <c>#goto</c> needs to
/// resolve a destination room that lives in a zone other than the loaded one.
/// <see cref="ZoneRoomIndex"/> maps each room's server-room-id (game-wide unique)
/// and title to its zone + node across every zone in the Maps folder.
/// </summary>
public class ZoneRoomIndexTests
{
    private static MapZone Zone(string name, params (int id, string serverId, string title)[] rooms)
    {
        var z = new MapZone { Name = name };
        foreach (var (id, serverId, title) in rooms)
            z.Nodes[id] = new MapNode { Id = id, ServerRoomId = serverId, Title = title };
        return z;
    }

    private static ZoneRoomIndex TwoZones()
        => ZoneRoomIndex.Build(new[]
        {
            ("Map01_Crossing", Zone("Crossing",
                (1, "100", "Town Green"),
                (2, "101", "Bank Lobby"))),
            ("Map61_Leth_Deriel", Zone("Leth Deriel",
                (127, "200", "Old Crank's Road, Forest"),
                (179, "201", "Old Crank's Road, Forest"))),   // dup title, same zone
        });

    [Fact]
    public void Resolve_ServerRoomId_FindsCorrectZoneAndNode()
    {
        var idx = TwoZones();
        Assert.True(idx.TryResolveServerRoom("200", out var r));
        Assert.Equal("Map61_Leth_Deriel", r.Zone);
        Assert.Equal(127, r.NodeId);

        Assert.True(idx.TryResolveServerRoom("101", out var b));
        Assert.Equal("Map01_Crossing", b.Zone);
        Assert.Equal(2, b.NodeId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("999")]        // not indexed
    public void Resolve_UnknownOrBlank_ReturnsFalse(string? serverId)
    {
        Assert.False(TwoZones().TryResolveServerRoom(serverId, out _));
    }

    [Fact]
    public void ByTitle_ReturnsAllMatches_AcrossDuplicates()
    {
        var hits = TwoZones().ByTitle("Old Crank's Road, Forest");
        Assert.Equal(2, hits.Count);
        Assert.All(hits, h => Assert.Equal("Map61_Leth_Deriel", h.Zone));
    }

    [Fact]
    public void Counts_ReflectIndexedContent()
    {
        var idx = TwoZones();
        Assert.Equal(4, idx.RoomCount);
        Assert.Equal(2, idx.ZoneCount);
        Assert.Empty(idx.ByTitle("No Such Room"));
    }

    [Fact]
    public void Build_FirstWriterWins_OnServerIdCollision()
    {
        var idx = ZoneRoomIndex.Build(new[]
        {
            ("ZoneA", Zone("A", (1, "500", "Room A"))),
            ("ZoneB", Zone("B", (9, "500", "Room B"))),   // same server id — collision
        });
        Assert.True(idx.TryResolveServerRoom("500", out var r));
        Assert.Equal("ZoneA", r.Zone);   // first writer kept
    }
}

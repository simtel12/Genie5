namespace Genie.Core.Mapper;

/// <summary>
/// Derives cross-zone <see cref="ZoneConnection"/> edges directly from Genie 4
/// map data, so the multi-zone pathfinder works without a hand-authored
/// <c>ZoneConnections.xml</c> (community maps don't ship one — they encode the
/// links in the rooms themselves).
///
/// <para><b>How zones link in the data:</b> a <b>border room</b> carries a
/// <see cref="MapNode.Notes"/> whose first <c>|</c>-segment is the neighbour
/// zone's <c>.xml</c> file (e.g. <c>note="Map8_Crossing_East_Gate.xml|E Gate|East"</c>),
/// plus a <b>destination-less directional arc</b> that is the move OUT of the zone
/// (<c>&lt;arc exit="east" move="east"/&gt;</c> — in-zone arcs carry a
/// <c>destination</c>). The entry room on the far side is the <b>reciprocal</b>
/// border room (one whose note points back), disambiguated by reciprocal
/// direction when several rooms note back (gate vs. battlements).</para>
///
/// <para>Rooms are referenced by node-id string — the same key the pathfinder uses
/// while traversing a zone. Only links that are noted from <i>both</i> sides
/// produce an edge, so a one-sided note never fabricates a route.</para>
/// </summary>
public static class ZoneConnectionDeriver
{
    private readonly record struct Border(MapNode Node, string TargetZone, Direction Dir, string Move);

    /// <summary>Derive bidirectional cross-zone connections from the given zones
    /// (pure — no I/O). Each entry pairs a zone-file basename with its zone.</summary>
    public static IReadOnlyList<ZoneConnection> Derive(IEnumerable<(string ZoneFile, MapZone Zone)> zones)
    {
        var byZone = new Dictionary<string, MapZone>(StringComparer.OrdinalIgnoreCase);
        foreach (var (zoneFile, zone) in zones)
            if (zone is not null) byZone[zoneFile] = zone;

        // Index every zone's border rooms up front so we can look up reciprocals.
        var borders = new Dictionary<string, List<Border>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (zoneFile, zone) in byZone)
        {
            var list = new List<Border>();
            foreach (var node in zone.Nodes.Values)
            {
                var target = TargetZoneFromNote(node.Notes);
                if (target is null) continue;
                var exit = CrossExit(node);
                if (exit is null) continue;                         // no move out → can't use it
                list.Add(new Border(node, target, exit.Direction, VerbOf(exit)));
            }
            if (list.Count > 0) borders[zoneFile] = list;
        }

        var conns = new List<ZoneConnection>();
        foreach (var (zoneFile, list) in borders)
        {
            foreach (var b in list)
            {
                if (!byZone.ContainsKey(b.TargetZone)) continue;    // neighbour not in Maps
                if (!borders.TryGetValue(b.TargetZone, out var backList)) continue;

                // Reciprocal border rooms in the target zone that note back to us.
                var candidates = backList
                    .Where(x => x.TargetZone.Equals(zoneFile, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (candidates.Count == 0) continue;                // one-sided note — skip

                var recip = candidates.Count == 1
                    ? candidates[0]
                    : candidates.FirstOrDefault(x => x.Dir == Reciprocal(b.Dir), candidates[0]);

                conns.Add(new ZoneConnection
                {
                    FromZone    = zoneFile,
                    FromRoom    = b.Node.Id.ToString(),
                    ToZone      = b.TargetZone,
                    ToRoom      = recip.Node.Id.ToString(),
                    Verb        = b.Move,
                    TransitType = "border",
                });
            }
        }
        return conns;
    }

    /// <summary>The cross-zone move out of a border room: the first destination-less
    /// arc (in-zone arcs carry a destination). Prefer a compass-directional one.</summary>
    private static MapExit? CrossExit(MapNode n)
        => n.Exits.FirstOrDefault(e => e.DestinationId is null && e.Direction != Direction.None)
           ?? n.Exits.FirstOrDefault(e => e.DestinationId is null);

    private static string VerbOf(MapExit e)
        => !string.IsNullOrWhiteSpace(e.MoveCommand)
            ? e.MoveCommand
            : e.Direction.ToString().ToLowerInvariant();

    /// <summary>Target zone basename from a border room's note, or null when the note
    /// isn't a cross-zone link. Format: <c>"Map8_….xml|Label|Label"</c>.</summary>
    private static string? TargetZoneFromNote(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;
        var first = notes.Split('|')[0].Trim();
        return first.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(first)
            : null;
    }

    private static Direction Reciprocal(Direction d) => d switch
    {
        Direction.North     => Direction.South,     Direction.South     => Direction.North,
        Direction.East      => Direction.West,      Direction.West      => Direction.East,
        Direction.NorthEast => Direction.SouthWest, Direction.SouthWest => Direction.NorthEast,
        Direction.NorthWest => Direction.SouthEast, Direction.SouthEast => Direction.NorthWest,
        Direction.Up        => Direction.Down,      Direction.Down      => Direction.Up,
        Direction.Out       => Direction.In,        Direction.In        => Direction.Out,
        _                   => Direction.None,
    };
}

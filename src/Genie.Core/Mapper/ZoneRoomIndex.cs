namespace Genie.Core.Mapper;

/// <summary>
/// A game-wide index of rooms across every zone in the Maps folder, so a
/// cross-zone <c>#goto</c> can resolve a destination that lives in a zone other
/// than the one currently loaded. Built by scanning the zone XMLs (the travel
/// data <b>is</b> the maps — each room carries a <c>&lt;nav rm="…"/&gt;</c>
/// server-room-id, and border rooms name their neighbour zone in
/// <see cref="MapNode.Notes"/>).
///
/// <para>Primary key is the <b>server-room-id</b> — numeric, unique game-wide,
/// stable across map regeneration — so it's the reliable cross-zone handle.
/// Room titles are indexed too (many-to-one) for name-based lookup.</para>
/// </summary>
public sealed class ZoneRoomIndex
{
    /// <summary>A room's location: the zone file basename (no extension) + node id.</summary>
    public readonly record struct RoomRef(string Zone, int NodeId);

    private readonly Dictionary<string, RoomRef> _byServerId;
    private readonly Dictionary<string, List<RoomRef>> _byTitle;

    private ZoneRoomIndex(Dictionary<string, RoomRef> byServerId,
                          Dictionary<string, List<RoomRef>> byTitle)
    {
        _byServerId = byServerId;
        _byTitle    = byTitle;
    }

    /// <summary>Number of distinct server-room-ids indexed.</summary>
    public int RoomCount => _byServerId.Count;

    /// <summary>Number of zones the index drew rooms from.</summary>
    public int ZoneCount { get; private init; }

    /// <summary>Resolve a server-room-id to its zone + node. Returns false when the
    /// id isn't in any indexed zone (unknown room, or its zone isn't in Maps).</summary>
    public bool TryResolveServerRoom(string? serverRoomId, out RoomRef room)
    {
        room = default;
        if (string.IsNullOrWhiteSpace(serverRoomId)) return false;
        return _byServerId.TryGetValue(serverRoomId.Trim(), out room);
    }

    /// <summary>All rooms whose title matches (case-insensitive). Empty when none.
    /// Titles are not unique — many rooms share "Old Crank's Road, Forest".</summary>
    public IReadOnlyList<RoomRef> ByTitle(string? title)
        => !string.IsNullOrWhiteSpace(title) &&
           _byTitle.TryGetValue(title.Trim(), out var list)
            ? list
            : [];

    /// <summary>Build the index from already-loaded zones (pure — no I/O). Each entry
    /// pairs a zone-file basename (no extension) with its <see cref="MapZone"/>.</summary>
    public static ZoneRoomIndex Build(IEnumerable<(string ZoneFile, MapZone Zone)> zones)
    {
        var byServerId = new Dictionary<string, RoomRef>(StringComparer.OrdinalIgnoreCase);
        var byTitle    = new Dictionary<string, List<RoomRef>>(StringComparer.OrdinalIgnoreCase);
        int zoneCount  = 0;

        foreach (var (zoneFile, zone) in zones)
        {
            if (zone is null) continue;
            zoneCount++;
            foreach (var node in zone.Nodes.Values)
            {
                var loc = new RoomRef(zoneFile, node.Id);

                // First writer wins on server-id collisions (shouldn't happen with
                // clean data; a dup means two zones claim the same room — keep the
                // first so lookups stay deterministic).
                if (!string.IsNullOrWhiteSpace(node.ServerRoomId))
                    byServerId.TryAdd(node.ServerRoomId.Trim(), loc);

                if (!string.IsNullOrWhiteSpace(node.Title))
                {
                    var key = node.Title.Trim();
                    if (!byTitle.TryGetValue(key, out var list))
                        byTitle[key] = list = new List<RoomRef>();
                    list.Add(loc);
                }
            }
        }

        return new ZoneRoomIndex(byServerId, byTitle) { ZoneCount = zoneCount };
    }

    /// <summary>Scan a Maps directory: enumerate + load every zone XML and index it.
    /// Unreadable zones are skipped. Returns an empty index when the directory is
    /// missing.</summary>
    public static ZoneRoomIndex Scan(MapZoneRepository repo, string mapsDirectory)
    {
        var loaded = new List<(string ZoneFile, MapZone Zone)>();
        foreach (var path in repo.ListZoneFiles(mapsDirectory))
        {
            var zone = repo.Load(path);
            if (zone is not null)
                loaded.Add((Path.GetFileNameWithoutExtension(path), zone));
        }
        return Build(loaded);
    }
}

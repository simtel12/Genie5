namespace Genie.Core.Mapper;

/// <summary>
/// Combines the note-derived cross-zone links (<see cref="ZoneConnectionDeriver"/>)
/// with any hand-authored <c>ZoneConnections.xml</c> entries for the multi-zone
/// pathfinder.
///
/// <para>Authored entries <b>augment</b> the derived graph — and override a
/// derived link only on an exact <c>(from-zone, from-room, to-zone, to-room)</c>
/// match — rather than replacing it wholesale. This matters because Genie seeds a
/// baseline <c>ZoneConnections.xml</c> of placeholder (<c>TODO</c>) rows on first
/// launch: under the old "authored wins if any exist" rule those placeholders
/// suppressed every working derived link, so the multi-zone pathfinder saw zero
/// usable cross-zone edges after the first run. Merging keeps the derived links
/// live; the placeholder rows are harmless (the pathfinder never matches a
/// <c>TODO</c> room id), and a single hand-added route no longer erases the rest
/// of the graph.</para>
/// </summary>
public static class ZoneConnectionMerge
{
    // Control-char (U+0001) separator between the four endpoint fields so distinct
    // tuples can't collide (e.g. "ab|c" vs "a|bc"); it never appears in a zone
    // basename or room id.
    private static readonly char Sep = (char)1;

    private static string Key(ZoneConnection c) =>
        string.Join(Sep, c.FromZone, c.FromRoom, c.ToZone, c.ToRoom).ToLowerInvariant();

    /// <summary>Derived links first (order preserved); each authored link either
    /// replaces the derived link with the same from/to endpoints — so a curated
    /// entry can supply real verb/wait metadata — or is appended as a new link.</summary>
    public static IReadOnlyList<ZoneConnection> Merge(
        IReadOnlyList<ZoneConnection> derived,
        IReadOnlyList<ZoneConnection> authored)
    {
        var result = new List<ZoneConnection>(derived);
        var index  = new Dictionary<string, int>();
        for (var i = 0; i < result.Count; i++) index[Key(result[i])] = i;

        foreach (var a in authored)
        {
            var k = Key(a);
            if (index.TryGetValue(k, out var i))
                result[i] = a;                        // authored overrides the derived link
            else
            {
                index[k] = result.Count;
                result.Add(a);                        // brand-new authored link
            }
        }

        return result;
    }
}

namespace Genie.Core.Mapper;

/// <summary>
/// Normalizes a map arc's move command into the actual game command to send.
///
/// <para>Genie 4 map data encodes non-game <b>pacing prefixes</b> on some arcs —
/// <c>move="rt north"</c>, <c>move="slow south"</c> — directives the old
/// <c>.automapper</c> movement script consumed (wait-for-roundtime, pause), NOT
/// DragonRealms verbs. Sent verbatim they return <i>"Please rephrase that
/// command."</i> and stall travel (public #123). We strip the known pacing
/// prefix and send the bare movement; Genie 5's walker already paces
/// one-move-per-room and gates on roundtime, so the directive's intent is
/// preserved. Real DR verbs (<c>go</c>, <c>climb</c>, <c>swim</c>, <c>dive</c>,
/// <c>search</c>) are left untouched.</para>
/// </summary>
public static class MoveVerb
{
    /// <summary>Leading tokens that are pacing directives, not DR verbs. Confirmed
    /// against live play: <c>rt</c> (wait-for-roundtime) and <c>slow</c> (#123) both
    /// return "Please rephrase that command." if sent. The Genie 4 automapper script
    /// recognised a larger family (<c>wait/room/web/muck/…</c>), but those need in-game
    /// confirmation before we strip them, so we start with the two confirmed ones.
    /// Add to this list as more are verified.</summary>
    private static readonly string[] PacingPrefixes = { "rt", "slow" };

    /// <summary>Strip a known pacing prefix from <paramref name="verb"/> (e.g.
    /// "slow south" → "south", "rt north" → "north"); return it unchanged if it
    /// carries no such prefix. Only strips when the prefix is followed by a space
    /// and a non-empty remainder, so "slower" or a bare "rt" are left alone.</summary>
    public static string Normalize(string? verb)
    {
        if (string.IsNullOrWhiteSpace(verb)) return verb ?? string.Empty;
        var v = verb.TrimStart();
        foreach (var p in PacingPrefixes)
            if (v.Length > p.Length + 1 &&
                v[p.Length] == ' ' &&
                v.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                return v[(p.Length + 1)..].TrimStart();
        return v;
    }
}

namespace Genie.Core.Analytics;

/// <summary>
/// Tolerant streaming reader over the analytics folder tree
/// (<c>{AnalyticsDir}\{Char}-{Acct}\*.jsonl</c>). Every line parses
/// independently; torn or foreign lines are skipped, so a crash-truncated
/// tail or a future row type never breaks history loading.
/// </summary>
public sealed class SkillHistoryStore
{
    private readonly string _root;

    /// <param name="analyticsRoot">The AnalyticsDir root (holds one folder per character).</param>
    public SkillHistoryStore(string analyticsRoot) => _root = analyticsRoot;

    /// <summary>Character folder slugs that have any history — feeds the
    /// dashboard's character picker.</summary>
    public IReadOnlyList<string> ListCharacters()
    {
        try
        {
            if (!Directory.Exists(_root)) return Array.Empty<string>();
            return Directory.GetDirectories(_root)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    public string DirectoryFor(string slug) => Path.Combine(_root, slug);

    /// <summary>Session summaries, oldest first, optionally windowed by start time.</summary>
    public IReadOnlyList<SessionRecord> LoadSessions(string slug, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var result = new List<SessionRecord>();
        foreach (var s in ReadAll<SessionRecord>(Path.Combine(DirectoryFor(slug), SkillHistoryWriter.SessionsFile)))
        {
            if (fromUtc is { } f && s.StartUtc < f) continue;
            if (toUtc   is { } t && s.StartUtc > t) continue;
            result.Add(s);
        }
        result.Sort((a, b) => a.StartUtc.CompareTo(b.StartUtc));
        return result;
    }

    /// <summary>All snapshots belonging to one session, in write order.</summary>
    public IReadOnlyList<SnapshotRecord> LoadSnapshots(string slug, string sessionId)
    {
        var result = new List<SnapshotRecord>();
        foreach (var snap in EnumerateSnapshots(slug))
            if (string.Equals(snap.SessionId, sessionId, StringComparison.Ordinal))
                result.Add(snap);
        return result;
    }

    /// <summary>Snapshots in a UTC time window, in write order.</summary>
    public IReadOnlyList<SnapshotRecord> LoadSnapshots(string slug, DateTime fromUtc, DateTime toUtc)
    {
        var result = new List<SnapshotRecord>();
        foreach (var snap in EnumerateSnapshots(slug, fromUtc, toUtc))
            if (snap.AtUtc >= fromUtc && snap.AtUtc <= toUtc)
                result.Add(snap);
        return result;
    }

    /// <summary>Daily rollups, oldest first, optionally windowed by day.</summary>
    public IReadOnlyList<DailyRecord> LoadDaily(string slug, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var result = new List<DailyRecord>();
        foreach (var d in ReadAll<DailyRecord>(Path.Combine(DirectoryFor(slug), SkillHistoryWriter.DailyFile)))
        {
            if (!DateTime.TryParse(d.Day, System.Globalization.CultureInfo.InvariantCulture,
                                   System.Globalization.DateTimeStyles.AssumeUniversal |
                                   System.Globalization.DateTimeStyles.AdjustToUniversal, out var day))
                continue;
            if (fromUtc is { } f && day < f.Date) continue;
            if (toUtc   is { } t && day > t.Date) continue;
            result.Add(d);
        }
        result.Sort((a, b) => string.CompareOrdinal(a.Day, b.Day));
        return result;
    }

    /// <summary>Snapshot shard paths for a slug, chronological (name order).</summary>
    public IReadOnlyList<string> SnapshotShards(string slug)
    {
        try
        {
            string dir = DirectoryFor(slug);
            if (!Directory.Exists(dir)) return Array.Empty<string>();
            return Directory.GetFiles(dir, "snapshots-*.jsonl")
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    private IEnumerable<SnapshotRecord> EnumerateSnapshots(string slug, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        foreach (var shard in SnapshotShards(slug))
        {
            // Skip whole shards outside the window — a shard named YYYYMM
            // covers [month, month+1). Cheap range pruning before any parsing.
            if (ShardMonth(shard) is { } month)
            {
                if (toUtc   is { } to   && month > to) continue;
                if (fromUtc is { } from && month.AddMonths(1) <= from) continue;
            }
            foreach (var snap in ReadAll<SnapshotRecord>(shard))
                yield return snap;
        }
    }

    /// <summary>First-of-month for a <c>snapshots-YYYYMM.jsonl</c> path, or null.</summary>
    internal static DateTime? ShardMonth(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);          // snapshots-202607
        int dash = name.LastIndexOf('-');
        if (dash < 0 || name.Length - dash - 1 != 6) return null;
        return DateTime.TryParseExact(name[(dash + 1)..], "yyyyMM",
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.AssumeUniversal |
                   System.Globalization.DateTimeStyles.AdjustToUniversal, out var m)
            ? m.Date : null;
    }

    private static IEnumerable<T> ReadAll<T>(string path) where T : class
    {
        if (!File.Exists(path)) yield break;
        IEnumerable<string> lines;
        try { lines = File.ReadLines(path); }
        catch { yield break; }
        foreach (var line in lines)
            if (SkillHistoryJson.TryParse<T>(line) is { } rec)
                yield return rec;
    }
}

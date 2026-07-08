namespace Genie.Core.Analytics;

/// <summary>
/// Compaction + retention for the skill-history store. Snapshot shards
/// entirely older than the retention window are folded into per-UTC-day
/// <see cref="DailyRecord"/> rollups (kept forever, ~1 KB/day) and deleted,
/// so long-horizon gain curves survive raw-data pruning.
///
/// <para>Idempotent by construction: each day is derived deterministically
/// from its shard's snapshots and REPLACES the same day key in
/// <c>daily.jsonl</c> — folding a shard twice yields the same file. A UTC day
/// lives in exactly one monthly shard, so replacement is safe.</para>
/// </summary>
public static class SkillHistoryRollup
{
    /// <summary>
    /// Derive day rollups from one shard's snapshots. Per skill: end-of-day
    /// rank/percent = last observation; gain = first→last observation that day
    /// (deltas between days land on the later day's first observation — a
    /// documented day-granularity approximation); secs = tracked span.
    /// </summary>
    public static List<DailyRecord> BuildDaily(IEnumerable<SnapshotRecord> snaps)
    {
        // day → (session ids, min/max elapsed per session, per-skill fold)
        var days = new SortedDictionary<string, DayFold>(StringComparer.Ordinal);

        foreach (var snap in snaps)
        {
            string key = snap.AtUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            if (!days.TryGetValue(key, out var fold))
                days[key] = fold = new DayFold();

            if (!fold.Sessions.TryGetValue(snap.SessionId, out var span))
                fold.Sessions[snap.SessionId] = span = new ElapsedSpan { Min = snap.Elapsed, Max = snap.Elapsed };
            span.Min = Math.Min(span.Min, snap.Elapsed);
            span.Max = Math.Max(span.Max, snap.Elapsed);

            foreach (var (skill, v) in snap.Skills)
            {
                if (v is not { Length: >= 2 }) continue;
                int mind = v.Length >= 3 ? v[2] : 0;
                if (!fold.Skills.TryGetValue(skill, out var sf))
                {
                    fold.Skills[skill] = sf = new SkillFold
                    {
                        FirstRank = v[0], FirstPercent = v[1],
                        FirstAt = snap.AtUtc,
                    };
                }
                sf.LastRank = v[0]; sf.LastPercent = v[1]; sf.LastMind = mind;
                sf.LastAt = snap.AtUtc;
            }
        }

        var result = new List<DailyRecord>(days.Count);
        foreach (var (day, fold) in days)
        {
            var rec = new DailyRecord
            {
                Day = day,
                Sessions = fold.Sessions.Count,
                Seconds = fold.Sessions.Values.Sum(s => Math.Max(0, s.Max - s.Min)),
            };
            foreach (var (skill, sf) in fold.Skills)
                rec.Skills[skill] = new DailySkill
                {
                    Rank = sf.LastRank,
                    Percent = sf.LastPercent,
                    Gain = Extensions.Builtin.ExperienceExtension.GainValue(
                        sf.LastRank, sf.LastPercent, sf.FirstRank, sf.FirstPercent),
                    Seconds = (long)Math.Max(0, (sf.LastAt - sf.FirstAt).TotalSeconds),
                };
            result.Add(rec);
        }
        return result;
    }

    /// <summary>Merge freshly-derived days into the existing rollup list —
    /// replace matching day keys, keep everything else, sort by day.</summary>
    public static List<DailyRecord> Merge(IReadOnlyList<DailyRecord> existing, IReadOnlyList<DailyRecord> derived)
    {
        var byDay = new SortedDictionary<string, DailyRecord>(StringComparer.Ordinal);
        foreach (var d in existing) if (d.Day.Length > 0) byDay[d.Day] = d;
        foreach (var d in derived)  if (d.Day.Length > 0) byDay[d.Day] = d;   // replace = idempotent
        return byDay.Values.ToList();
    }

    /// <summary>
    /// Apply retention to one character folder: every snapshot shard whose
    /// month lies entirely before <paramref name="nowUtc"/> −
    /// <paramref name="retentionDays"/> is folded into <c>daily.jsonl</c>
    /// (temp-file + atomic replace) and deleted. <c>retentionDays</c> ≤ 0 =
    /// keep raw snapshots forever (no-op). Returns the number of shards
    /// compacted. Never throws — a failed shard is skipped.
    /// </summary>
    public static int ApplyRetention(string characterDirectory, int retentionDays, DateTime nowUtc, Action<string>? log = null)
    {
        if (retentionDays <= 0) return 0;
        int compacted = 0;
        try
        {
            if (!Directory.Exists(characterDirectory)) return 0;
            var cutoff = nowUtc.Date.AddDays(-retentionDays);

            foreach (var shard in Directory.GetFiles(characterDirectory, "snapshots-*.jsonl")
                                           .OrderBy(f => f, StringComparer.Ordinal))
            {
                var month = SkillHistoryStore.ShardMonth(shard);
                if (month is null || month.Value.AddMonths(1) > cutoff) continue;   // not entirely old

                try
                {
                    var snaps = new List<SnapshotRecord>();
                    foreach (var line in File.ReadLines(shard))
                        if (SkillHistoryJson.TryParse<SnapshotRecord>(line) is { } s)
                            snaps.Add(s);

                    string dailyPath = Path.Combine(characterDirectory, SkillHistoryWriter.DailyFile);
                    var existing = new List<DailyRecord>();
                    if (File.Exists(dailyPath))
                        foreach (var line in File.ReadLines(dailyPath))
                            if (SkillHistoryJson.TryParse<DailyRecord>(line) is { } d)
                                existing.Add(d);

                    var merged = Merge(existing, BuildDaily(snaps));

                    // daily.jsonl stays small — rewrite whole via temp + replace
                    // so a crash mid-write can't lose the old rollups.
                    string tmp = dailyPath + ".tmp";
                    File.WriteAllLines(tmp, merged.Select(SkillHistoryJson.ToLine));
                    if (File.Exists(dailyPath)) File.Replace(tmp, dailyPath, null);
                    else File.Move(tmp, dailyPath);

                    File.Delete(shard);
                    compacted++;
                }
                catch (Exception ex)
                {
                    log?.Invoke($"[analytics] compaction of {Path.GetFileName(shard)} failed ({ex.Message}) — will retry next run.");
                }
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"[analytics] retention pass failed ({ex.Message}).");
        }
        return compacted;
    }

    private sealed class DayFold
    {
        public Dictionary<string, ElapsedSpan> Sessions { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, SkillFold>   Skills   { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ElapsedSpan { public long Min; public long Max; }

    private sealed class SkillFold
    {
        public int FirstRank; public int FirstPercent; public DateTime FirstAt;
        public int LastRank;  public int LastPercent;  public int LastMind; public DateTime LastAt;
    }
}

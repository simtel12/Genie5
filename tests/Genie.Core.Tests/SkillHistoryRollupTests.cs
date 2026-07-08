using System;
using System.IO;
using System.Linq;
using Genie.Core.Analytics;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Skill-history compaction/retention — daily rollups derive deterministically
/// from snapshots (so folding a shard twice is idempotent), retention deletes
/// only shards entirely past the window, and daily.jsonl survives via the
/// temp-file + atomic-replace rewrite.
/// </summary>
public class SkillHistoryRollupTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "Genie5Test-rollup-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static SnapshotRecord Snap(string sid, DateTime at, long el, params (string Skill, int R, int P)[] skills)
    {
        var s = new SnapshotRecord { SessionId = sid, AtUtc = at, Elapsed = el };
        foreach (var (skill, r, p) in skills) s.Skills[skill] = new[] { r, p, 5 };
        return s;
    }

    [Fact]
    public void BuildDaily_FoldsPerUtcDay()
    {
        var d1 = new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc);
        var d2 = new DateTime(2026, 1, 6, 10, 0, 0, DateTimeKind.Utc);
        var days = SkillHistoryRollup.BuildDaily(new[]
        {
            Snap("s1", d1,                 0,    ("Small Edged", 100, 0)),
            Snap("s1", d1.AddHours(1),     3600, ("Small Edged", 101, 50)),
            Snap("s2", d1.AddHours(5),     0,    ("Attunement", 10, 0)),
            Snap("s2", d1.AddHours(6),     3600, ("Attunement", 11, 0)),
            Snap("s3", d2,                 0,    ("Small Edged", 101, 50)),
            Snap("s3", d2.AddHours(2),     7200, ("Small Edged", 103, 0)),
        });

        Assert.Equal(2, days.Count);

        var day1 = days.Single(d => d.Day == "2026-01-05");
        Assert.Equal(2, day1.Sessions);
        Assert.Equal(7200, day1.Seconds);                       // 3600 tracked per session
        Assert.Equal(1.5, day1.Skills["Small Edged"].Gain, 2);  // 100.00 → 101.50
        Assert.Equal(101, day1.Skills["Small Edged"].Rank);
        Assert.Equal(1.0, day1.Skills["Attunement"].Gain, 2);

        var day2 = days.Single(d => d.Day == "2026-01-06");
        Assert.Equal(1, day2.Sessions);
        Assert.Equal(1.5, day2.Skills["Small Edged"].Gain, 2);  // 101.50 → 103.00
        Assert.Equal(103, day2.Skills["Small Edged"].Rank);
    }

    [Fact]
    public void Merge_ReplacesDayKeys_Idempotently()
    {
        var existing = new[]
        {
            new DailyRecord { Day = "2026-01-05", Sessions = 9 },   // stale — will be replaced
            new DailyRecord { Day = "2026-01-01", Sessions = 1 },   // untouched
        };
        var derived = new[] { new DailyRecord { Day = "2026-01-05", Sessions = 2 } };

        var once  = SkillHistoryRollup.Merge(existing, derived);
        var twice = SkillHistoryRollup.Merge(once, derived);

        Assert.Equal(2, once.Count);
        Assert.Equal(2, once.Single(d => d.Day == "2026-01-05").Sessions);
        Assert.Equal(once.Select(d => (d.Day, d.Sessions)), twice.Select(d => (d.Day, d.Sessions)));
    }

    [Fact]
    public void ApplyRetention_FoldsOldShards_KeepsRecent_Idempotent()
    {
        Directory.CreateDirectory(_dir);
        var writer = new SkillHistoryWriter(_dir);
        var now = new DateTime(2026, 7, 7, 12, 0, 0, DateTimeKind.Utc);

        var old = new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc);       // way past 90 days
        writer.WriteSnapshot(Snap("s1", old,             0,    ("Small Edged", 100, 0)));
        writer.WriteSnapshot(Snap("s1", old.AddHours(1), 3600, ("Small Edged", 101, 50)));

        var recent = now.AddDays(-3);                                          // inside the window
        writer.WriteSnapshot(Snap("s9", recent, 0, ("Attunement", 5, 0)));

        int compacted = SkillHistoryRollup.ApplyRetention(_dir, retentionDays: 90, nowUtc: now);
        Assert.Equal(1, compacted);
        Assert.False(File.Exists(Path.Combine(_dir, "snapshots-202601.jsonl")));   // folded + deleted
        Assert.True(File.Exists(Path.Combine(_dir, SkillHistoryWriter.SnapshotFileFor(recent))));

        var store = new SkillHistoryStore(Path.GetDirectoryName(_dir)!);
        var daily = store.LoadDaily(Path.GetFileName(_dir));
        var day = Assert.Single(daily);
        Assert.Equal("2026-01-05", day.Day);
        Assert.Equal(1.5, day.Skills["Small Edged"].Gain, 2);

        // Second pass: nothing left to compact, daily unchanged.
        Assert.Equal(0, SkillHistoryRollup.ApplyRetention(_dir, 90, now));
        Assert.Equal(daily.Count, store.LoadDaily(Path.GetFileName(_dir)).Count);
    }

    [Fact]
    public void ApplyRetention_ZeroDays_KeepsRawForever()
    {
        Directory.CreateDirectory(_dir);
        var writer = new SkillHistoryWriter(_dir);
        writer.WriteSnapshot(Snap("s1", new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), 0, ("X", 1, 0)));

        Assert.Equal(0, SkillHistoryRollup.ApplyRetention(_dir, retentionDays: 0,
            nowUtc: new DateTime(2026, 7, 7, 0, 0, 0, DateTimeKind.Utc)));
        Assert.True(File.Exists(Path.Combine(_dir, "snapshots-202001.jsonl")));
    }
}

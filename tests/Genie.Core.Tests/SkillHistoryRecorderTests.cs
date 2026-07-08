using System;
using System.IO;
using System.Linq;
using Genie.Core.Analytics;
using Genie.Core.Config;
using Genie.Core.Runtime;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Skill-history recorder — accumulate-under-lock + flush-on-interval
/// semantics (driven here via the internal hooks, no timer waits), session
/// summary math (GainValue parity with the Experience window), and the
/// replay / master-toggle gates.
/// </summary>
public class SkillHistoryRecorderTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "Genie5Test-rec-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private GenieConfig NewConfig()
    {
        var cfg = new GenieConfig(new LocalDirectoryService("Genie5Test", AppContext.BaseDirectory));
        cfg.SetSetting("analyticsdir", _root, showException: false);   // rooted → used as-is
        return cfg;
    }

    private static SkillHistoryRecorder NewRecorder(GenieConfig cfg, bool isReplay = false) =>
        new(cfg, experience: null, connectionState: null,
            characterName: "Tirost", accountName: "ACCT", isReplay: isReplay);

    [Fact]
    public void Flush_WritesOnlyPendingDeltas_ThenGoesQuiet()
    {
        var cfg = NewConfig();
        using var rec = NewRecorder(cfg);

        rec.BeginSession();
        rec.OnSkillUpdated("Small Edged", 100, 10, 5);
        rec.OnSkillUpdated("Attunement", 550, 74, 3);
        rec.OnSkillUpdated("Small Edged", 100, 25, 7);   // supersedes within the window

        var snap = rec.FlushNow();
        Assert.NotNull(snap);
        Assert.Equal(2, snap!.Skills.Count);              // one row per skill, latest values
        Assert.Equal(new[] { 100, 25, 7 }, snap.Skills["Small Edged"]);

        Assert.Null(rec.FlushNow());                      // nothing pending → no row
    }

    [Fact]
    public void EndSession_WritesSummary_WithGainParityMath()
    {
        var cfg = NewConfig();
        using (var rec = NewRecorder(cfg))
        {
            rec.BeginSession();
            rec.OnTdpUpdated(3017);
            rec.OnSkillUpdated("Small Edged", 100, 34, 5);   // baseline
            rec.OnSkillUpdated("Small Edged", 101, 5, 9);    // later
            rec.OnTdpUpdated(3042);
            rec.EndSession();
        }

        var store = new SkillHistoryStore(_root);
        var slug = Assert.Single(store.ListCharacters());
        Assert.Equal("Tirost-ACCT", slug);

        var session = Assert.Single(store.LoadSessions(slug));
        Assert.Equal(3017, session.TdpStart);
        Assert.Equal(3042, session.TdpEnd);
        Assert.False(session.Recovered);
        var span = session.Skills["Small Edged"];
        // +0.71: same fractional math as the Experience window's gain column.
        Assert.Equal(0.71, span.Gain, 2);

        // The final flush before the summary captured the last delta.
        var snaps = store.LoadSnapshots(slug, session.Id);
        Assert.True(snaps.Count >= 1);
        Assert.Equal(new[] { 101, 5, 9 }, snaps.Last().Skills["Small Edged"]);
    }

    [Fact]
    public void EndSession_IsIdempotent_AndDisposeIsSafeAfter()
    {
        var cfg = NewConfig();
        var rec = NewRecorder(cfg);
        rec.BeginSession();
        rec.OnSkillUpdated("X", 1, 0, 1);
        rec.EndSession();
        rec.EndSession();     // second close: no duplicate summary
        rec.Dispose();        // dispose after close: no third

        var store = new SkillHistoryStore(_root);
        Assert.Single(store.LoadSessions("Tirost-ACCT"));
    }

    [Fact]
    public void SessionWithNoSkillData_WritesNoSummary()
    {
        var cfg = NewConfig();
        using (var rec = NewRecorder(cfg))
        {
            rec.BeginSession();
            rec.EndSession();
        }
        var store = new SkillHistoryStore(_root);
        // A connect that never trained anything isn't history worth keeping.
        Assert.True(store.ListCharacters().Count == 0
                    || store.LoadSessions("Tirost-ACCT").Count == 0);
    }

    [Fact]
    public void ReplayWithoutOptIn_RecordsNothing()
    {
        var cfg = NewConfig();                              // analyticsreplay defaults off
        using (var rec = NewRecorder(cfg, isReplay: true))
        {
            rec.BeginSession();
            rec.OnSkillUpdated("X", 1, 0, 1);
            Assert.Null(rec.FlushNow());
            rec.EndSession();
        }
        Assert.False(Directory.Exists(Path.Combine(_root, "Tirost-ACCT")));
    }

    [Fact]
    public void ReplayWithOptIn_RecordsRowsMarkedReplay()
    {
        var cfg = NewConfig();
        cfg.SetSetting("analyticsreplay", "True", showException: false);
        using (var rec = NewRecorder(cfg, isReplay: true))
        {
            rec.BeginSession();
            rec.OnSkillUpdated("X", 1, 0, 1);
            rec.EndSession();
        }
        var store = new SkillHistoryStore(_root);
        var session = Assert.Single(store.LoadSessions("Tirost-ACCT"));
        Assert.True(session.Replay);
        Assert.All(store.LoadSnapshots("Tirost-ACCT", session.Id), s => Assert.True(s.Replay));
    }

    [Fact]
    public void MasterToggleOff_RecordsNothing_LiveMidSession()
    {
        var cfg = NewConfig();
        using var rec = NewRecorder(cfg);
        rec.BeginSession();
        rec.OnSkillUpdated("X", 1, 0, 1);
        Assert.NotNull(rec.FlushNow());

        cfg.SetSetting("analytics", "False", showException: false);   // live gate
        rec.OnSkillUpdated("X", 2, 0, 1);
        Assert.Null(rec.FlushNow());                                  // gated — nothing accumulated
    }

    [Fact]
    public void LiveSessionSnapshot_ReflectsInFlightState()
    {
        var cfg = NewConfig();
        using var rec = NewRecorder(cfg);
        Assert.Null(rec.LiveSessionSnapshot());

        rec.BeginSession();
        rec.OnSkillUpdated("Small Edged", 100, 0, 5);
        rec.OnSkillUpdated("Small Edged", 102, 50, 9);

        var live = rec.LiveSessionSnapshot();
        Assert.NotNull(live);
        Assert.Equal(2.5, live!.Skills["Small Edged"].Gain, 2);
    }
}

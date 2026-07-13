using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genie.Core.Scripting;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Engine-level coverage for the #script management surface (Genie 4
/// FormMain.Command_Script* parity): the Genie 4-format status listing,
/// the local-variable dump with filter, the rolling control-flow trace,
/// per-script pause/resume toggling, and hot reload at the next goto.
/// </summary>
public class ScriptManagementTests : IDisposable
{
    private readonly string _dir;
    private readonly List<string> _echoed = new();
    private readonly ScriptEngine _engine;

    public ScriptManagementTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "gc_scriptmgmt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _engine = new ScriptEngine(_dir, new TypeAheadSession(),
                                   sendCommand: _ => { }, echo: l => _echoed.Add(l));
    }

    public void Dispose()
    {
        _engine.StopAll();
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private void Start(string name, string body)
    {
        File.WriteAllText(Path.Combine(_dir, name + ".cmd"), body);
        Assert.True(_engine.TryStart(name, Array.Empty<string>()));
    }

    private void Pump(int ticks = 50)
    {
        for (int i = 0; i < ticks; i++) _engine.Tick();
    }

    [Fact]
    public void StatusLines_report_state_file_and_paused_marker()
    {
        Start("blocker", ":TOP\nwaitfor NEVERCOMES\n");
        Pump();

        var line = Assert.Single(_engine.StatusLines(null));
        Assert.StartsWith("blocker:", line);
        Assert.Contains(" seconds. WaitFor (blocker.cmd)", line);

        _engine.PauseScript("blocker");
        line = Assert.Single(_engine.StatusLines("blocker"));
        Assert.StartsWith("blocker(Paused):", line);
        Assert.Contains("Paused (blocker.cmd)", line);

        _engine.ResumeScript("blocker");
        _engine.SetTrace("blocker", 5);
        line = Assert.Single(_engine.StatusLines(null));
        Assert.Contains("[Debuglevel: 5]", line);

        // Exact-name filter: no match → empty, "all" → everything.
        Assert.Empty(_engine.StatusLines("nosuch"));
        Assert.Single(_engine.StatusLines("all"));
    }

    [Fact]
    public void VarsLines_dump_locals_and_honor_the_filter()
    {
        Start("vartest", "var prey goblin\nvar weapon scimitar\nwaitfor NEVERCOMES\n");
        Pump();

        var all = _engine.VarsLines("vartest", string.Empty);
        Assert.Contains(all, l => l.Trim() == "prey=goblin");
        Assert.Contains(all, l => l.Trim() == "weapon=scimitar");
        Assert.StartsWith("vartest:", all[0]);   // status header first

        var filtered = _engine.VarsLines("vartest", "prey");
        Assert.Contains(filtered, l => l.Trim() == "prey=goblin");
        Assert.DoesNotContain(filtered, l => l.Contains("weapon="));
    }

    [Fact]
    public void Trace_records_control_flow_oldest_first()
    {
        Start("tracer",
            ":TOP\n" +
            "gosub SUB\n" +
            "goto MID\n" +
            ":SUB\n" +
            "return\n" +
            ":MID\n" +
            "waitfor NEVERCOMES\n");
        Pump();

        var dump = _engine.TraceDumpLines("tracer");
        Assert.StartsWith("tracer:", dump[0]);
        var entries = dump.Skip(1).Select(l => l.Trim()).ToList();
        Assert.Collection(entries,
            e => Assert.StartsWith("passing label TOP", e),
            e => Assert.StartsWith("gosub SUB", e),
            e => Assert.StartsWith("return", e),
            e => Assert.StartsWith("goto MID", e));
        // Genie 4 entry format carries the origin file and source line.
        Assert.Matches(@"gosub SUB tracer\(\d+\)", entries[1]);
    }

    [Fact]
    public void PauseOrResume_toggles_each_matching_script()
    {
        Start("toggler", ":TOP\nwaitfor NEVERCOMES\n");
        Pump();

        _engine.PauseOrResume("toggler");
        Assert.Contains("[script] toggler paused", _echoed);
        Assert.Contains("(Paused)", _engine.StatusLines(null)[0]);

        _engine.PauseOrResume(null);   // all-form flips it back
        Assert.Contains("[script] toggler resumed", _echoed);
        Assert.DoesNotContain("(Paused)", _engine.StatusLines(null)[0]);
    }

    [Fact]
    public void RequestReload_marks_once_and_warns_on_repeat()
    {
        Start("pending", ":TOP\nwaitfor NEVERCOMES\n");
        Pump();

        _engine.RequestReload("pending");
        Assert.Contains("[script] pending will be reloaded at the next label.", _echoed);

        _engine.RequestReload(null);   // all-form hits the already-pending script
        Assert.Contains("[script] pending is already pending reload at the next label.", _echoed);

        _engine.RequestReload("nosuch");
        Assert.Contains("[script] no running script named nosuch", _echoed);
    }

    [Fact]
    public void HotReload_at_next_goto_picks_up_edits_and_keeps_vars()
    {
        Start("hot",
            "var keep me\n" +
            ":TOP\n" +
            "echo V1 %keep\n" +
            "waitfor GO\n" +
            "goto TOP\n");
        Pump();
        Assert.Contains("V1 me", _echoed);

        _engine.RequestReload("hot");
        File.WriteAllText(Path.Combine(_dir, "hot.cmd"),
            "var keep clobbered\n" +          // start-of-file line does NOT re-run
            ":TOP\n" +
            "echo V2 %keep\n" +
            "waitfor GO\n" +
            "goto TOP\n");

        _engine.OnGameLine("GO");             // releases waitfor → goto → hot reload
        Pump();

        Assert.Contains(_echoed, l => l.Contains("reloaded from disk (continuing at 'TOP')"));
        Assert.Contains("V2 me", _echoed);    // new text, old variable value
        Assert.Single(_engine.StatusLines("hot"));
    }

    [Fact]
    public void GetStatuses_reports_structured_cmd_fields()
    {
        Start("snap",
            ":TOP\n" +            // line 1
            "gosub SUB\n" +       // line 2
            "waitfor NEVERCOMES\n" +
            ":SUB\n" +            // line 4
            "match one FOO\n" +
            "match two BAR\n" +
            "matchwait\n");       // line 7 — blocks inside the gosub
        Pump();

        var s = Assert.Single(_engine.GetStatuses());
        Assert.Equal("snap", s.Name);
        Assert.False(s.IsJs);
        Assert.False(s.Paused);
        Assert.Equal("MatchWait", s.State);
        Assert.Equal(7, s.CurrentLine);           // the blocking matchwait line
        Assert.Equal(2, s.PendingMatchCount);
        Assert.Equal(1, s.GosubDepth);
        Assert.Equal(0, s.DebugLevel);
        Assert.False(s.PendingReload);
        Assert.EndsWith("snap.cmd", s.SourcePath);
        Assert.True(s.ElapsedSeconds >= 0);

        _engine.PauseScript("snap");
        _engine.SetTrace("snap", 3);
        _engine.RequestReload("snap");
        s = Assert.Single(_engine.GetStatuses());
        Assert.True(s.Paused);
        Assert.Equal("Paused", s.State);
        Assert.Equal(3, s.DebugLevel);
        Assert.True(s.PendingReload);
    }

    [Fact]
    public void GetStatuses_and_StatusLines_agree()
    {
        Start("agree", ":TOP\nwaitfor NEVERCOMES\n");
        Pump();

        var snap = Assert.Single(_engine.GetStatuses());
        var line = Assert.Single(_engine.StatusLines(null));
        Assert.Contains(snap.State, line);
        Assert.Contains(Path.GetFileName(snap.SourcePath), line);
    }

    [Fact]
    public void HotReload_stops_the_script_when_the_label_is_gone()
    {
        Start("doomed",
            ":TOP\n" +
            "waitfor GO\n" +
            "goto TOP\n");
        Pump();

        _engine.RequestReload("doomed");
        File.WriteAllText(Path.Combine(_dir, "doomed.cmd"),
            ":ELSEWHERE\nwaitfor GO\n");

        _engine.OnGameLine("GO");
        Pump();

        Assert.Contains(_echoed, l => l.Contains("hot reload failed at 'TOP'"));
        Assert.Empty(_engine.StatusLines("doomed"));
    }
}

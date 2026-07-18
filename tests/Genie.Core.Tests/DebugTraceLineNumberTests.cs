using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genie.Core.Scripting;
using Xunit;
using Xunit.Abstractions;

namespace Genie.Core.Tests;

/// <summary>
/// The dbg-level-1 control-flow trace (`goto X → line N`, `gosub X → line N`,
/// `return → line N`) must report the real SOURCE line number of the jump
/// target, not the compiled-array index. inst.Lines is the compiled program —
/// block braces split lines and blanks/comments shift positions, so the old
/// "index + 1" misreported every target (Jason's mm_train dbg:10: `gosub
/// mech.forage → line 1034` when the label was at source line 878, `goto
/// mainloop → line 268` for a label at 250). Regression from a real live trace.
/// </summary>
public class DebugTraceLineNumberTests
{
    private readonly ITestOutputHelper _out;
    public DebugTraceLineNumberTests(ITestOutputHelper o) => _out = o;

    private static List<string> Run(string body)
    {
        var echoed = new List<string>();
        var dir = Path.Combine(Path.GetTempPath(), "gc_dbgline_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "t.cmd"), body);
            var engine = new ScriptEngine(dir, new TypeAheadSession(),
                                          sendCommand: _ => { }, echo: l => echoed.Add(l));
            engine.TryStart("t", new List<string>());
            for (int i = 0; i < 300; i++) engine.Tick();
            return echoed;
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Goto_gosub_return_report_source_line_numbers()
    {
        // Block braces + comments make the compiled index diverge from the
        // source line (as in mm_train). Line numbers below are 1-based source
        // lines of THIS string (line 1 = "debug 1").
        //  1 debug 1
        //  2 # comment
        //  3 # comment
        //  4 gosub sub
        //  5 echo BACK
        //  6 exit
        //  7 (blank)
        //  8 sub:
        //  9 echo IN_SUB       <- gosub resumes here (line 9)
        // 10 return            <- return resumes at line 5 (echo BACK)
        const string body =
            "debug 1\n" +
            "# comment\n" +
            "# comment\n" +
            "gosub sub\n" +
            "echo BACK\n" +
            "exit\n" +
            "\n" +
            "sub:\n" +
            "echo IN_SUB\n" +
            "return\n";

        var o = Run(body);
        foreach (var l in o) _out.WriteLine(l);

        var gosub = o.FirstOrDefault(l => l.Contains("gosub sub"));
        var ret   = o.FirstOrDefault(l => l.StartsWith("return") || l.Contains("return →"));

        Assert.NotNull(gosub);
        Assert.NotNull(ret);
        // sub: is at source line 8; execution resumes at line 9 (echo IN_SUB).
        Assert.Contains("→ line 9", gosub);
        // return goes back to the line after `gosub sub` — line 5 (echo BACK).
        Assert.Contains("→ line 5", ret);
        // The old bug printed the array index (+1), which for these targets is
        // NOT 9 / 5 (comments/blank shift the compiled positions).
        Assert.DoesNotContain("→ line 8", gosub!);
    }

    [Fact]
    public void Goto_reports_the_resume_line_after_the_label()
    {
        //  1 debug 1
        //  2 goto start
        //  3 # skipped
        //  4 # skipped
        //  5 (blank)
        //  6 start:
        //  7 echo AT_START     <- goto resumes here (line 7)
        //  8 exit
        const string body =
            "debug 1\n" +
            "goto start\n" +
            "# skipped\n" +
            "# skipped\n" +
            "\n" +
            "start:\n" +
            "echo AT_START\n" +
            "exit\n";

        var o = Run(body);
        foreach (var l in o) _out.WriteLine(l);

        var gotoLine = o.FirstOrDefault(l => l.Contains("goto start"));
        Assert.NotNull(gotoLine);
        Assert.Contains("→ line 7", gotoLine);
        Assert.Contains("AT_START", o);
    }
}

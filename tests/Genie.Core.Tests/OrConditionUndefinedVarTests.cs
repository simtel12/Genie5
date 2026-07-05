using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genie.Core.Scripting;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// #133: an `if` with an || whose one side references an unset %var must still
/// evaluate the defined side. The unset var substitutes to "" before parsing,
/// so the condition arrives as "((1 = 1) || ( = 1))" — Genie 4 reads the empty
/// slot as the empty string ("" = "1" → false) and the || yields true. Before
/// the fix the empty slot was a parse error that failed the whole condition to
/// false (post-b056b29: with a warning), eating the defined side.
/// </summary>
public class OrConditionUndefinedVarTests
{
    private static List<string> RunFixture(string body)
    {
        var echoed = new List<string>();
        var dir = Path.Combine(Path.GetTempPath(), "gc_ortest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "t.cmd"), body);
            var engine = new ScriptEngine(dir, new TypeAheadSession(),
                                          sendCommand: _ => { }, echo: l => echoed.Add(l));
            engine.TryStart("t", new List<string>());
            for (int i = 0; i < 200; i++) engine.Tick();
            return echoed;
        }
        finally { try { Directory.Delete(dir, true); } catch { /* best effort */ } }
    }

    [Fact]
    public void Or_with_one_undefined_side_takes_the_defined_side()
    {
        // The community repro, verbatim shape: %kcast intentionally never set.
        var o = RunFixture(
            "var pcast 1\n" +
            "if ((%pcast = 1) || (%kcast = 1)) then echo ONEYES\n" +
            "else echo BOTHNO\n");

        Assert.Contains("ONEYES", o);
        Assert.DoesNotContain("BOTHNO", o);
        Assert.DoesNotContain(o, l => l.Contains("bad condition"));
    }

    [Fact]
    public void Undefined_side_alone_is_false_without_warning()
    {
        var o = RunFixture(
            "if (%kcast = 1) then echo KYES\n" +
            "else echo KNO\n");

        Assert.Contains("KNO", o);
        Assert.DoesNotContain("KYES", o);
        Assert.DoesNotContain(o, l => l.Contains("bad condition"));
    }

    [Fact]
    public void Or_false_when_both_sides_false()
    {
        // Guard the other direction: the empty slot must not read as true.
        var o = RunFixture(
            "var pcast 2\n" +
            "if ((%pcast = 1) || (%kcast = 1)) then echo ONEYES\n" +
            "else echo BOTHNO\n");

        Assert.Contains("BOTHNO", o);
        Assert.DoesNotContain("ONEYES", o);
    }
}

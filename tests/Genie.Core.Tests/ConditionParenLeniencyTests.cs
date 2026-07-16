using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genie.Core.Scripting;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Genie 4 parity for unbalanced parentheses in conditions, both directions.
/// G4's evaluator ignored a stray trailing ')' and implicitly closed unfinished
/// groups at end of text, and long-lived community scripts depend on both:
/// travel.cmd's ferry / currency-exchange / bank checks are all missing a close
/// (<c>if ("%lirums" != "0" then</c>), and uber-script loop exits carry an
/// extra one (<c>if (%i >= %n) && (%loop = 0)) then</c>). Those branches were
/// dead in G5 (parse error → false forever). The engine now retries once with
/// the parens balanced (quote-aware) and warns once per source; the #debug
/// if-trace carries the marker on every evaluation.
/// </summary>
public class ConditionParenLeniencyTests
{
    private static List<string> RunFixture(string body)
    {
        var echoed = new List<string>();
        var dir = Path.Combine(Path.GetTempPath(), "gc_lenient_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "t.cmd"), body);
            var engine = new ScriptEngine(dir, new TypeAheadSession(),
                                          sendCommand: _ => { }, echo: l => echoed.Add(l));
            engine.TryStart("t", new List<string>());
            for (int i = 0; i < 400; i++) engine.Tick();
            return echoed;
        }
        finally { try { Directory.Delete(dir, true); } catch { /* best effort */ } }
    }

    [Fact]
    public void Stray_trailing_close_evaluates_g4_style_inline()
    {
        // The uber-script MAXCHECK shape: missing outer open, stray trailing ')'.
        var o = RunFixture(
            "var i 15\n" +
            "var loop 0\n" +
            "if (%i >= 15) && (%loop = 0)) then echo HIT\n" +
            "echo done\n");

        Assert.Contains("HIT", o);
        Assert.Contains("done", o);
        Assert.Contains(o, l => l.Contains("auto-balanced"));
    }

    [Fact]
    public void Stray_trailing_close_block_form_takes_the_branch()
    {
        var o = RunFixture(
            "var i 15\n" +
            "var loop 0\n" +
            "if (%i >= 15) && (%loop = 0)) then\n" +
            "{\n" +
            "     echo HIT\n" +
            "}\n" +
            "echo done\n");

        Assert.Contains("HIT", o);
        Assert.Contains("done", o);
    }

    [Fact]
    public void Stray_trailing_close_false_condition_skips_the_branch()
    {
        var o = RunFixture(
            "var i 5\n" +
            "var loop 0\n" +
            "if (%i >= 15) && (%loop = 0)) then\n" +
            "{\n" +
            "     echo HIT\n" +
            "}\n" +
            "echo done\n");

        Assert.DoesNotContain("HIT", o);
        Assert.Contains("done", o);
    }

    [Fact]
    public void Missing_close_after_function_call_ferry_shape()
    {
        // travel.cmd:2258 — parens inside the quoted zone list must not
        // confuse the balance counter.
        var o = RunFixture(
            "var zoneid 7\n" +
            "if contains(\"(1|7|30|35)\",\"%zoneid\" then echo FERRY\n" +
            "echo done\n");

        Assert.Contains("FERRY", o);
        Assert.Contains("done", o);
        Assert.Contains(o, l => l.Contains("auto-balanced"));
    }

    [Fact]
    public void Missing_close_quoted_neq_shape()
    {
        // travel.cmd:2786 — the currency-exchange gate.
        var o = RunFixture(
            "var lirums 5\n" +
            "if (\"%lirums\" != \"0\" then echo EXCHANGE\n" +
            "echo done\n");

        Assert.Contains("EXCHANGE", o);
        Assert.Contains("done", o);
    }

    [Fact]
    public void Close_paren_inside_quotes_is_text_not_structure()
    {
        // Naive counting would read ")abc" as a surplus close and give up;
        // quote-aware counting sees one missing close and appends it.
        var o = RunFixture(
            "var x )abc\n" +
            "if (\"%x\" = \")abc\" then echo HIT\n" +
            "echo done\n");

        Assert.Contains("HIT", o);
        Assert.Contains("done", o);
    }

    [Fact]
    public void Auto_balance_warns_once_per_line_per_run()
    {
        var o = RunFixture(
            "var i 0\n" +
            "top:\n" +
            "math i add 1\n" +
            "if (%i >= 99) && (0 = 0)) then echo NEVER\n" +
            "if (%i < 4) then goto top\n" +
            "echo done\n");

        Assert.Contains("done", o);
        Assert.Equal(1, o.Count(l => l.Contains("auto-balanced")));
    }

    [Fact]
    public void Debug_trace_marks_every_lenient_evaluation()
    {
        // Unlike the once-per warning, the #debug if-trace carries the marker
        // on EVERY pass — a stuck loop gets looked at hours after the warning
        // scrolled away.
        var o = RunFixture(
            "debug 5\n" +
            "var i 0\n" +
            "top:\n" +
            "math i add 1\n" +
            "if (%i >= 99) && (0 = 0)) then echo NEVER\n" +
            "if (%i < 4) then goto top\n" +
            "echo done\n");

        Assert.Contains("done", o);
        Assert.True(o.Count(l => l.Contains("[dbg") && l.Contains("auto-balanced")) >= 3,
            "every lenient evaluation should carry the trace marker");
    }
}

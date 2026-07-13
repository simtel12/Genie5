using System;
using System.Collections.Generic;
using System.IO;
using Genie.Core.Scripting;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Genie 4 parity for two reported scripting-engine bugs, driven end-to-end
/// through the real engine:
///   • #128 — nested / stacked variables resolve inside-out (right-to-left),
///     so "%harness%counter" and "$%output" evaluate like Genie 4, and the
///     "%%name" / "$$name" double-eval keeps working.
///   • #129 — def()/defined() sees the persistent #var store (via UserVarLookup)
///     and uses existence (not non-empty) semantics, matching Genie 4's
///     VariableList.ContainsKey.
/// </summary>
public class ScriptNestedVarAndDefTests
{
    private static List<string> RunFixture(string body, Func<string, string?>? userVars = null,
                                           IDictionary<string, string>? globals = null)
    {
        var echoed = new List<string>();
        var dir = Path.Combine(Path.GetTempPath(), "gc_nesttest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "t.cmd"), body);
            var engine = new ScriptEngine(dir, new TypeAheadSession(),
                                          sendCommand: _ => { }, echo: l => echoed.Add(l));
            if (userVars is not null) engine.UserVarLookup = userVars;
            if (globals is not null)
                foreach (var (k, v) in globals) engine.Globals[k] = v;
            engine.TryStart("t", new List<string>());
            for (int i = 0; i < 200; i++) engine.Tick();
            return echoed;
        }
        finally { try { Directory.Delete(dir, true); } catch { /* best effort */ } }
    }

    [Fact]
    public void Nested_variables_resolve_inside_out()   // #128
    {
        const string body =
            "var counter 1\n" +
            "var harness1 loaded\n" +
            "var output var1\n" +
            "echo A=%harness%counter\n" +   // %counter→1 ⇒ %harness1 ⇒ loaded
            "echo B=$%output\n";            // %output→var1 ⇒ $var1 ⇒ foo (a GLOBAL —
                                            // $ never reads script locals, G4 parity)

        var o = RunFixture(body, globals: new Dictionary<string, string> { ["var1"] = "foo" });

        Assert.Contains("A=loaded", o);
        Assert.Contains("B=foo",    o);
    }

    [Fact]
    public void Double_eval_still_works()   // #128 regression guard
    {
        const string body =
            "var spell fire\n" +
            "var fire active\n" +
            "echo D=%%spell\n";             // inner %spell→fire ⇒ %fire ⇒ active

        Assert.Contains("D=active", RunFixture(body));
    }

    [Fact]
    public void Double_eval_with_dot_suffix_matches_genie4_docs()   // #128 regression guard
    {
        // The community-documented Genie 4 example (right-to-left evaluation):
        //   var spell Fireball
        //   var Fireball.Prep 10
        //   echo %%spell.Prep   → inner %spell.Prep shrink-resolves to %spell
        //                         ("Fireball"), forming %Fireball.Prep ⇒ 10.
        const string body =
            "var spell Fireball\n" +
            "var Fireball.Prep 10\n" +
            "echo P=%%spell.Prep\n";

        Assert.Contains("P=10", RunFixture(body));
    }

    [Fact]
    public void Array_index_still_works()   // #128 regression guard
    {
        const string body =
            "var bags sack|pouch|crate\n" +
            "var i 1\n" +
            "echo E=%bags(%i)\n";           // index resolves first, then element 1

        Assert.Contains("E=pouch", RunFixture(body));
    }

    [Fact]
    public void Def_sees_the_uservar_store()   // #129
    {
        // Mirrors the issue repro: `#var testvar foo` lives in the persistent
        // #var store (UserVarLookup), NOT locals/live-globals. def(testvar)
        // must still see it.
        const string body =
            "if (def(testvar)) then echo DEFINED\n" +
            "else echo NOTDEFINED\n" +
            "if (def(nope)) then echo N_DEFINED\n" +
            "else echo N_NOTDEFINED\n";

        var o = RunFixture(body, name => name == "testvar" ? "foo" : null);

        Assert.Contains("DEFINED",      o);
        Assert.DoesNotContain("NOTDEFINED", o);
        Assert.Contains("N_NOTDEFINED", o);
    }

    [Fact]
    public void Def_true_even_when_uservar_is_empty()   // #129 existence, not non-empty
    {
        const string body =
            "if (def(blank)) then echo DEFINED\n" +
            "else echo NOTDEFINED\n";

        // Exists in the #var store but set to "" — Genie 4 ContainsKey ⇒ defined.
        var o = RunFixture(body, name => name == "blank" ? "" : null);

        Assert.Contains("DEFINED", o);
        Assert.DoesNotContain("NOTDEFINED", o);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Genie.Core.Scripting;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Genie 4 parity for the argument system (Group C of the parity audit), driven
/// end-to-end through the real engine: %argcount/$argcount, the empty-fill of
/// unpassed args, if_N = "argcount &gt;= N", and shift's count decrement + %0
/// rebuild + vacated-slot clear.
/// </summary>
public class ScriptEngineArgTests
{
    private static List<string> RunFixture(string body, IReadOnlyList<string> args)
    {
        var echoed = new List<string>();
        var dir = Path.Combine(Path.GetTempPath(), "gc_argtest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "argtest.cmd"), body);
            var engine = new ScriptEngine(dir, new TypeAheadSession(),
                                          sendCommand: _ => { }, echo: l => echoed.Add(l));
            engine.TryStart("argtest", args);
            for (int i = 0; i < 200; i++) engine.Tick();   // pump to completion
            return echoed;
        }
        finally { try { Directory.Delete(dir, true); } catch { /* best effort */ } }
    }

    [Fact]
    public void Argcount_ifN_and_shift_match_Genie4()
    {
        const string body =
            "echo AC=%argcount\n" +
            "echo DAC=$argcount\n" +
            "echo A2=%2\n" +
            "echo A5=[%5]\n" +
            "if_2 then echo IF2T\n" +
            "if_3 then echo IF3T\n" +
            "shift\n" +
            "echo SAC=%argcount\n" +
            "echo S0=%0\n" +
            "echo S1=%1\n" +
            "echo S2=[%2]\n";

        var o = RunFixture(body, new List<string> { "a", "b" });   // 2 args

        Assert.Contains("AC=2",  o);    // %argcount
        Assert.Contains("DAC=2", o);    // $argcount
        Assert.Contains("A2=b",  o);    // %2 = 2nd arg
        Assert.Contains("A5=[]", o);    // %5 missing ⇒ "" (G4 fill)
        Assert.Contains("IF2T",  o);    // if_2: argcount(2) >= 2
        Assert.DoesNotContain("IF3T", o); // if_3: argcount(2) >= 3 is false
        Assert.Contains("SAC=1", o);    // shift decremented argcount
        Assert.Contains("S0=b",  o);    // %0 rebuilt from remaining args
        Assert.Contains("S1=b",  o);    // old %2 shifted into %1
        Assert.Contains("S2=[]", o);    // vacated top slot cleared to ""
    }

    [Fact]
    public void Gosub_args_are_quote_aware_not_space_split()
    {
        // Genie 4 parity: a quoted multi-word gosub arg ("Moonmage Training Menu")
        // must arrive as a SINGLE $-arg with its outer quotes stripped. A plain
        // Split(' ') shattered it across $4/$5/$6 and leaked stray quotes into the
        // neighbouring args — which is what made menu scripts (mm_train) spray a
        // fresh window per option and mangle the click commands.
        const string body =
            "gosub sub \"alpha beta\" plain \"gamma\"\n" +
            "exit\n" +
            "sub:\n" +
            "echo D1=[$1]\n" +
            "echo D2=[$2]\n" +
            "echo D3=[$3]\n" +
            "echo D4=[$4]\n" +
            "return\n";

        var o = RunFixture(body, new List<string>());

        Assert.Contains("D1=[alpha beta]", o);  // quoted multi-word ⇒ one arg, quotes stripped
        Assert.Contains("D2=[plain]",      o);  // bare arg unaffected
        Assert.Contains("D3=[gamma]",      o);  // quoted single word ⇒ quotes stripped
        Assert.Contains("D4=[]",           o);  // unpassed arg ⇒ empty
    }

    [Fact]
    public void Do_command_is_guarded_and_not_sent_to_game()
    {
        // Group D is deferred (zero corpus usage); the no-op guard must consume a
        // stray `do` line — warn, don't leak "do …" to the game — and let the
        // script keep running.
        var echoed = new List<string>();
        var sent = new List<string>();
        var dir = Path.Combine(Path.GetTempPath(), "gd_doguard_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "dotest.cmd"),
                "do clear\n" +
                "do hunt orc\n" +
                "echo AFTER\n");
            var engine = new ScriptEngine(dir, new TypeAheadSession(),
                                          sendCommand: c => sent.Add(c), echo: l => echoed.Add(l));
            engine.TryStart("dotest", new List<string>());
            for (int i = 0; i < 200; i++) engine.Tick();

            Assert.Equal(2, echoed.FindAll(l => l.Contains("'do' command is not supported")).Count);
            Assert.DoesNotContain(sent, c => c.Contains("hunt") || c.Contains("clear"));
            Assert.Contains("AFTER", echoed);   // script continued past the do lines
        }
        finally { try { Directory.Delete(dir, true); } catch { /* best effort */ } }
    }
}

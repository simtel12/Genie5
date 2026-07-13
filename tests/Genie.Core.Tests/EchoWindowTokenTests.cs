using System;
using System.Collections.Generic;
using System.IO;
using Genie.Core.Commanding;
using Genie.Core.Scripting;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// The <c>#echo &gt;window</c> token: the parser strips ALL leading '&gt;'
/// chars before the name reaches the window layer. Driven end-to-end through
/// the script engine's <c>put #echo</c> path (the one alert scripts use with
/// a <c>&gt;$alertwindow</c> target variable) plus the typed-command parser.
/// Covers the field report where a target variable whose value already
/// carried the chevron ("var w &gt;Log") expanded to "&gt;&gt;Log" and
/// manufactured a junk window literally named "&gt;Log" — the token now
/// degrades gracefully to "Log".
/// </summary>
public class EchoWindowTokenTests
{
    private static List<(string msg, string? window)> RunFixture(
        string body, IDictionary<string, string>? globals = null)
    {
        var calls = new List<(string, string?)>();
        var dir = Path.Combine(Path.GetTempPath(), "gc_echotest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "t.cmd"), body);
            var engine = new ScriptEngine(dir, new TypeAheadSession(),
                                          sendCommand: _ => { }, echo: _ => { });
            // $vars are GLOBALS-only (Genie 4 Script.cs:2368 — the $ branch
            // resolves via m_oGlobals, never script locals). Alert scripts set
            // $alertwindow with #var/#tvar; the fixture seeds the same store.
            if (globals is not null)
                foreach (var (k, v) in globals) engine.Globals[k] = v;
            engine.EchoTo = (msg, win, _) => calls.Add((msg, win));
            engine.TryStart("t", new List<string>());
            for (int i = 0; i < 200; i++) engine.Tick();
            return calls;
        }
        finally { try { Directory.Delete(dir, true); } catch { /* best effort */ } }
    }

    [Fact]
    public void Window_token_strips_the_chevron()
    {
        var calls = RunFixture(
            "put #echo >$w hello\n",
            new Dictionary<string, string> { ["w"] = "Log" });

        Assert.Contains(("hello", "Log"), calls);
    }

    [Fact]
    public void Quoted_multiword_window_token_routes_to_that_window()
    {
        // The classic menu-script form (mm_train GlobalSet):
        //   send #echo ">Moonmage Training Menu" cyan Enter value for CHARGE:
        // The quoted multi-word target must parse as the window token; before
        // this was quote-aware the token started with '"', fell out of the
        // option loop, and the whole line (quotes and all) dumped to main.
        var calls = RunFixture(
            "put #echo \">$w\" cyan Enter value for CHARGE:\n",
            new Dictionary<string, string> { ["w"] = "Moonmage Training Menu" });

        Assert.Contains(("Enter value for CHARGE:", "Moonmage Training Menu"), calls);
    }

    [Fact]
    public void Quoted_window_with_no_message_echoes_a_blank_line_there()
    {
        // mm_train separates menu sections with `send #echo ">%this.window"`.
        var calls = RunFixture(
            "put #echo \">Moonmage Training Menu\"\n");

        Assert.Contains(("", "Moonmage Training Menu"), calls);
    }

    [Fact]
    public void Chevron_inside_the_variable_value_degrades_gracefully()
    {
        // var value already carries '>': the token expands to ">>Log"; all
        // leading chevrons are trimmed so the echo still reaches "Log"
        // instead of manufacturing a junk window named ">Log".
        var calls = RunFixture(
            "put #echo >$w hello\n",
            new Dictionary<string, string> { ["w"] = ">Log" });

        Assert.Contains(("hello", "Log"), calls);
    }

    [Fact]
    public void Typed_command_parser_trims_repeated_chevrons()
    {
        EchoArgs.Parse(new[] { "echo", ">>Log", "hi" }, 1,
                       out var window, out _, out _, out var msg);

        Assert.Equal("Log", window);
        Assert.Equal("hi", msg);
    }

    [Fact]
    public void Typed_command_parser_strips_the_chevron_too()
    {
        EchoArgs.Parse(new[] { "echo", ">Log", "hi", "there" }, 1,
                       out var window, out var color, out var mono, out var msg);

        Assert.Equal("Log", window);
        Assert.Null(color);
        Assert.False(mono);
        Assert.Equal("hi there", msg);
    }
}

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
    private static List<(string msg, string? window)> RunFixture(string body)
    {
        var calls = new List<(string, string?)>();
        var dir = Path.Combine(Path.GetTempPath(), "gc_echotest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "t.cmd"), body);
            var engine = new ScriptEngine(dir, new TypeAheadSession(),
                                          sendCommand: _ => { }, echo: _ => { });
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
            "var w Log\n" +
            "put #echo >$w hello\n");

        Assert.Contains(("hello", "Log"), calls);
    }

    [Fact]
    public void Chevron_inside_the_variable_value_degrades_gracefully()
    {
        // var value already carries '>': the token expands to ">>Log"; all
        // leading chevrons are trimmed so the echo still reaches "Log"
        // instead of manufacturing a junk window named ">Log".
        var calls = RunFixture(
            "var w >Log\n" +
            "put #echo >$w hello\n");

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

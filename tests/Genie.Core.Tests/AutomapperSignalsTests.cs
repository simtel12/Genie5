using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Genie.Core.Mapper;
using Genie.Core.Scripting;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// #96 — the attended walker's <c>#goto</c> outcome signals are a hard
/// backwards-compatibility contract with ~19 community movement scripts. These
/// tests lock the exact wording against the regexes those scripts arm, and prove
/// the end-to-end path a script actually uses: <c>matchre</c> + <c>matchwait</c>
/// around a <c>#goto</c>, resumed when the walker injects the signal via
/// <see cref="ScriptEngine.OnGameLine"/> (which is how the App's AutoWalkService
/// delivers them through <c>_core.Scripts.OnGameLine</c>).
/// </summary>
public class AutomapperSignalsTests
{
    // The canonical waiters, copied verbatim from the community travel.cmd:
    //   matchre AUTOMOVE_FAILED    ^(?:AUTOMAPPER )?MOVE(?:MENT)? FAILED
    //   matchre AUTOMOVE_RETURN    ^YOU HAVE ARRIVED(?:\!)?
    //   matchre AUTOMOVE_FAIL_BAIL ^DESTINATION NOT FOUND
    private const string ArrivedRe   = @"^YOU HAVE ARRIVED(?:\!)?";
    private const string FailedRe    = @"^(?:AUTOMAPPER )?MOVE(?:MENT)? FAILED";
    private const string NotFoundRe  = @"^DESTINATION NOT FOUND";

    [Fact]
    public void Arrived_signal_satisfies_the_community_matchre()
    {
        Assert.Matches(ArrivedRe, AutomapperSignals.Arrived);
    }

    [Fact]
    public void MovementFailed_signal_satisfies_the_community_matchre()
    {
        Assert.Matches(FailedRe, AutomapperSignals.MovementFailed);
    }

    [Fact]
    public void DestinationNotFound_signal_satisfies_the_community_matchre()
    {
        Assert.Matches(NotFoundRe, AutomapperSignals.DestinationNotFound);
    }

    [Fact]
    public void Signals_are_line_start_anchored_no_leading_prefix()
    {
        // The regexes anchor with ^, so any leading text (a stream tag, a prompt)
        // would break the match. Guard that the constants begin with the token.
        Assert.StartsWith("YOU HAVE ARRIVED", AutomapperSignals.Arrived, StringComparison.Ordinal);
        Assert.StartsWith("AUTOMAPPER MOVEMENT FAILED", AutomapperSignals.MovementFailed, StringComparison.Ordinal);
        Assert.StartsWith("DESTINATION NOT FOUND", AutomapperSignals.DestinationNotFound, StringComparison.Ordinal);
        // The three are distinct, so a script can route arrive vs fail vs bail.
        Assert.NotEqual(AutomapperSignals.Arrived, AutomapperSignals.MovementFailed);
        Assert.NotEqual(AutomapperSignals.MovementFailed, AutomapperSignals.DestinationNotFound);
    }

    // ── End-to-end: a script's #goto matchwait resumes on the injected signal ──

    private sealed class Harness : IDisposable
    {
        public readonly ScriptEngine Engine;
        public readonly List<string> Echoed = new();
        private readonly string _dir;

        public Harness(string body)
        {
            _dir = Path.Combine(Path.GetTempPath(), "gc_autosig_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "auto.cmd"), body);
            Engine = new ScriptEngine(_dir, new TypeAheadSession(),
                                      sendCommand: _ => { }, echo: l => Echoed.Add(l));
        }

        public void Pump(int ticks = 50) { for (int i = 0; i < ticks; i++) Engine.Tick(); }
        public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }
    }

    [Theory]
    [InlineData("^YOU HAVE ARRIVED(?:\\!)?")]      // travel.cmd AUTOMOVE_RETURN
    [InlineData("^(?:AUTOMAPPER )?MOVE(?:MENT)? FAILED")] // AUTOMOVE_FAILED
    [InlineData("^DESTINATION NOT FOUND")]          // AUTOMOVE_FAIL_BAIL
    public void Matchwait_blocks_until_the_walker_signal_arrives(string waitRe)
    {
        // Mirror the community idiom: arm the matchre, matchwait, then fall
        // through to a marker line only reachable once the match fires.
        var body =
            "echo BEFORE\n" +
            $"matchre DONE {waitRe}\n" +
            "matchwait\n" +
            "DONE:\n" +
            "echo RESUMED\n";

        using var h = new Harness(body);
        h.Engine.TryStart("auto", Array.Empty<string>());

        h.Pump();                                   // run up to the matchwait
        Assert.Contains("BEFORE",  h.Echoed);
        Assert.DoesNotContain("RESUMED", h.Echoed); // still blocked — no signal yet

        // The walker delivers the outcome exactly like AutoWalkService does.
        h.Engine.OnGameLine(SignalFor(waitRe));
        h.Pump();

        Assert.Contains("RESUMED", h.Echoed);       // matchwait resumed at the label
    }

    // Map each waiter regex to the constant the walker would actually emit — the
    // whole point is that the emitted constant satisfies the armed regex.
    private static string SignalFor(string waitRe) => waitRe switch
    {
        ArrivedRe  => AutomapperSignals.Arrived,
        FailedRe   => AutomapperSignals.MovementFailed,
        NotFoundRe => AutomapperSignals.DestinationNotFound,
        _          => throw new ArgumentOutOfRangeException(nameof(waitRe), waitRe, "no signal mapped"),
    };
}

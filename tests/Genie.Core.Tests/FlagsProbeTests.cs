using System;
using System.Collections.Generic;
using System.Linq;
using Genie.Core.Events;
using Genie.Core.Parser;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// #29 — the connect-time `flags` probe. When armed with
/// <see cref="DrXmlParser.BeginFlagsCaptureWindow"/>, the parser folds the DR
/// `flags` verb's plain-text table into one <see cref="FlagsReportEvent"/> and
/// suppresses every report line from display; a user-typed `flags` (window not
/// armed) is untouched and shows normally.
/// </summary>
public class FlagsProbeTests
{
    private sealed class Collector : IObserver<GameEvent>
    {
        private readonly List<GameEvent> _sink;
        public Collector(List<GameEvent> sink) => _sink = sink;
        public void OnNext(GameEvent e) => _sink.Add(e);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    // Verbatim from a live capture (2026-07-09), trimmed to the shape the parser sees.
    private const string FlagsOutput =
        "Usage\n" +
        "  FLAG {flag_name} {on|off}\n" +
        "Flag names may be abbreviated.\n" +
        "Example\n" +
        "  FLAG LOGON ON\n" +
        "  FLAG LOGON OFF\n" +
        "  Flag            Status  Behavior for this setting\n" +
        "  LogOn              ON   Show logon messages.\n" +
        "  RoomNames          ON   Display the name of the room in which you are located.\n" +
        "  Description        ON   Display room descriptions.\n" +
        "  RoomBrief          OFF  Display the full text of the room description.\n" +
        "  MonsterBold        ON   Highlight monster names.\n" +
        "  StatusPrompt       ON   Display status information in front of the prompt.\n" +
        "  ConciseThoughts    OFF  Gweth messages will be longer.\n" +
        "  ShowRoomID         ON   You are seeing approved room IDs when LOOKing.\n" +
        "For other setting options, see AVOID, SET, and TOGGLE.\n";

    private static (List<GameEvent> events, DrXmlParser parser) Feed(bool arm, string text)
    {
        var parser = new DrXmlParser(NullLogger<DrXmlParser>.Instance);
        var events = new List<GameEvent>();
        using var _ = parser.GameEvents.Subscribe(new Collector(events));
        if (arm) parser.BeginFlagsCaptureWindow();
        parser.Feed(text);
        return (events, parser);
    }

    [Fact]
    public void Armed_probe_emits_one_report_with_parsed_states()
    {
        var (events, _) = Feed(arm: true, FlagsOutput);

        var report = Assert.Single(events.OfType<FlagsReportEvent>());
        Assert.True(report.Flags["RoomNames"]);
        Assert.True(report.Flags["Description"]);
        Assert.False(report.Flags["RoomBrief"]);       // OFF
        Assert.True(report.Flags["MonsterBold"]);
        Assert.True(report.Flags["StatusPrompt"]);
        Assert.False(report.Flags["ConciseThoughts"]); // OFF
        Assert.True(report.Flags["ShowRoomID"]);
    }

    [Fact]
    public void Armed_probe_suppresses_every_report_line_from_display()
    {
        var (events, _) = Feed(arm: true, FlagsOutput);

        // No flag row, header, or boilerplate line should have reached display.
        foreach (var te in events.OfType<TextEvent>())
        {
            Assert.DoesNotContain("Behavior for this setting", te.Text);
            Assert.DoesNotContain("RoomBrief", te.Text);
            Assert.DoesNotContain("For other setting options", te.Text);
            Assert.DoesNotContain("FLAG {flag_name}", te.Text);
        }
    }

    [Fact]
    public void Unarmed_flags_output_displays_normally_and_emits_no_report()
    {
        var (events, _) = Feed(arm: false, FlagsOutput);

        Assert.Empty(events.OfType<FlagsReportEvent>());
        // The table renders as ordinary text when the user typed `flags`.
        Assert.Contains(events.OfType<TextEvent>(), te => te.Text.Contains("RoomBrief"));
    }

    [Fact]
    public void Interleaved_game_line_after_the_report_is_not_swallowed()
    {
        var (events, _) = Feed(arm: true, FlagsOutput + "Ghost Stealyr just arrived.\n");

        Assert.Single(events.OfType<FlagsReportEvent>());
        Assert.Contains(events.OfType<TextEvent>(), te => te.Text.Contains("Ghost Stealyr just arrived."));
    }

    [Fact]
    public void The_flag_example_line_is_not_mistaken_for_a_row()
    {
        // "FLAG LOGON ON" (an Example line) must not be parsed as LogOn=ON; the
        // real LogOn row supplies the state. Guard: first token must be a known
        // flag name, and "FLAG" is not one.
        var (events, _) = Feed(arm: true, FlagsOutput);
        var report = events.OfType<FlagsReportEvent>().Single();
        // 8 real rows in the fixture (LogOn + 7 others); the two "FLAG LOGON …"
        // example lines and the header are not counted.
        Assert.Equal(8, report.Flags.Count);
    }
}

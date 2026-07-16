using System;
using System.Collections.Generic;
using System.Linq;
using Genie.Core.Events;
using Genie.Core.Parser;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Public #174 — the roomname preset never painted the room title. DR frames
/// the title as a &lt;style id="roomName"/&gt; toggle that closes on the LINE
/// AFTER the title, so the span used to be recorded only after the title line
/// had already flushed (and then computed zero length against the fresh
/// buffer). FlushTextLine now splits an open style toggle at the flush
/// boundary, the same treatment open &lt;preset&gt; blocks get.
/// </summary>
public class StyleToggleSpanTests
{
    private static List<TextEvent> Parse(string xml)
    {
        var parser = new DrXmlParser(NullLogger<DrXmlParser>.Instance);
        var lines = new List<TextEvent>();
        using var _ = parser.GameEvents.Subscribe(new Collector(e =>
        {
            if (e is TextEvent t) lines.Add(t);
        }));
        parser.Feed(xml);
        return lines;
    }

    private static string Slice(string text, PresetSpan s) => text.Substring(s.Start, s.Length);

    private sealed class Collector : IObserver<GameEvent>
    {
        private readonly Action<GameEvent> _on;
        public Collector(Action<GameEvent> on) => _on = on;
        public void OnNext(GameEvent e) => _on(e);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    [Fact]
    public void Title_line_carries_roomName_span_when_toggle_closes_next_line()
    {
        // Exactly how DR frames a look response (from the live recordings).
        var lines = Parse(
            "<resource picture=\"0\"/><style id=\"roomName\" />[Test Room, Somewhere]\n" +
            "<style id=\"\"/>Nothing to see here.\n");

        var title = Assert.Single(lines, l => l.Text.Contains("[Test Room"));
        var span  = Assert.Single(title.PresetSpans ?? Array.Empty<PresetSpan>());
        Assert.Equal("roomName", span.PresetId);
        Assert.Equal("[Test Room, Somewhere]", Slice(title.Text, span));
    }

    [Fact]
    public void Line_after_the_title_gets_no_roomName_span()
    {
        // The close toggle lands at position 0 of the next line's buffer —
        // the zero-length remainder must not leak a span onto that line.
        var lines = Parse(
            "<style id=\"roomName\" />[Test Room, Somewhere]\n" +
            "<style id=\"\"/>Nothing to see here.\n");

        var after = Assert.Single(lines, l => l.Text.Contains("Nothing to see"));
        Assert.DoesNotContain(after.PresetSpans ?? Array.Empty<PresetSpan>(),
                              s => s.PresetId == "roomName");
    }

    [Fact]
    public void Toggle_spanning_a_flush_colours_both_line_tails()
    {
        // Open mid-line-1, close mid-line-2: line 1 gets a tail span from the
        // open position, line 2 a head span from the re-anchored 0.
        var lines = Parse(
            "plain <style id=\"roomName\" />styled one\n" +
            "styled two<style id=\"\"/> plain again\n");

        Assert.Equal(2, lines.Count);
        var s1 = Assert.Single(lines[0].PresetSpans!);
        Assert.Equal("styled one", Slice(lines[0].Text, s1));
        var s2 = Assert.Single(lines[1].PresetSpans!);
        Assert.Equal("styled two", Slice(lines[1].Text, s2));
    }

    [Fact]
    public void Same_line_toggle_still_yields_exactly_one_span()
    {
        // Open and close on one raw line: the close records the span and
        // resets the toggle, so the flush split must not add a duplicate.
        var lines = Parse("<style id=\"roomName\" />[Title]<style id=\"\"/> and more text\n");

        var line = Assert.Single(lines);
        var span = Assert.Single(line.PresetSpans!, s => s.PresetId == "roomName");
        Assert.Equal("[Title]", Slice(line.Text, span));
    }
}

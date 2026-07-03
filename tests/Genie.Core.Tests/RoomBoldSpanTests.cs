using System;
using System.Collections.Generic;
using Genie.Core.Events;
using Genie.Core.Parser;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// #131 Room-panel MonsterBold — the parser emits per-character bold spans for
/// the `room objs` component, as offsets into the decoded Content. Unlike the
/// creature-counting BoldNames (which extends to the next comma/period), these
/// must cover the EXACT &lt;pushBold&gt; text so the Room panel golds the
/// creature and nothing else.
/// </summary>
public class RoomBoldSpanTests
{
    private static (string content, IReadOnlyList<BoldSpan> spans) Parse(string roomObjsInner)
    {
        var parser = new DrXmlParser(NullLogger<DrXmlParser>.Instance);
        string content = "";
        IReadOnlyList<BoldSpan>? spans = null;
        using var _ = parser.GameEvents.Subscribe(new Collector(e =>
        {
            if (e is ComponentEvent c && c.ComponentId == "room objs")
            {
                content = c.Content;
                spans   = c.BoldSpans;
            }
        }));
        parser.Feed($"<component id='room objs'>{roomObjsInner}</component>");
        return (content, spans ?? Array.Empty<BoldSpan>());
    }

    private static string Slice(string content, BoldSpan s) => content.Substring(s.Start, s.Length);

    private sealed class Collector : IObserver<GameEvent>
    {
        private readonly Action<GameEvent> _on;
        public Collector(Action<GameEvent> on) => _on = on;
        public void OnNext(GameEvent e) => _on(e);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    [Fact]
    public void Span_covers_only_the_creature_not_trailing_scenery()
    {
        var (content, spans) = Parse("You also see <pushBold/>a ship's rat<popBold/> and a poster.");
        var s = Assert.Single(spans);
        Assert.Equal("a ship's rat", Slice(content, s));   // NOT "a ship's rat and a poster"
    }

    [Fact]
    public void Two_creatures_map_to_their_exact_spans()
    {
        var (content, spans) = Parse(
            "You also see <pushBold/>a town guard<popBold/> and <pushBold/>a grizzled old war veteran<popBold/>.");
        Assert.Equal(2, spans.Count);
        Assert.Equal("a town guard",                Slice(content, spans[0]));
        Assert.Equal("a grizzled old war veteran",  Slice(content, spans[1]));
    }

    [Fact]
    public void Duplicate_creatures_map_in_order_to_distinct_positions()
    {
        var (content, spans) = Parse(
            "You also see <pushBold/>a giant viper<popBold/> and <pushBold/>a giant viper<popBold/>.");
        Assert.Equal(2, spans.Count);
        Assert.Equal("a giant viper", Slice(content, spans[0]));
        Assert.Equal("a giant viper", Slice(content, spans[1]));
        Assert.True(spans[1].Start > spans[0].Start);   // second maps forward, not onto the first
    }

    [Fact]
    public void No_creatures_yields_no_spans()
    {
        var (_, spans) = Parse("You also see a wooden sign and a glowing forge.");
        Assert.Empty(spans);
    }
}

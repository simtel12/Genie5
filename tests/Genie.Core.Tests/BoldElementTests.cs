using System;
using System.Collections.Generic;
using System.Linq;
using Genie.Core.Events;
using Genie.Core.Parser;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// #160 — DR sends header emphasis in help text (e.g. PROFILE HELP) as a paired
/// HTML-style <c>&lt;b&gt;…&lt;/b&gt;</c> element, distinct from the self-closing
/// <c>&lt;pushBold/&gt;</c>/<c>&lt;popBold/&gt;</c> marker pair. Before the fix the
/// parser classified <c>&lt;b&gt;</c> as Unknown: it kept the text but dropped the
/// bold AND fired a spurious UnknownTagEvent (a false `#audit xmlhunting` report on
/// every PROFILE HELP). The element is now consumed as a bold span.
/// </summary>
public class BoldElementTests
{
    private sealed class Collector : IObserver<GameEvent>
    {
        private readonly List<GameEvent> _sink;
        public Collector(List<GameEvent> sink) => _sink = sink;
        public void OnNext(GameEvent e) => _sink.Add(e);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    private static List<GameEvent> Feed(params string[] chunks)
    {
        var parser = new DrXmlParser(NullLogger<DrXmlParser>.Instance);
        var events = new List<GameEvent>();
        using var _ = parser.GameEvents.Subscribe(new Collector(events));
        foreach (var chunk in chunks) parser.Feed(chunk);
        return events;
    }

    [Fact]
    public void B_is_classified_as_consumed_not_unknown()
    {
        Assert.Equal(DrXmlParser.TagFate.Consumed, DrXmlParser.ClassifyTag("b"));
        Assert.Equal(DrXmlParser.TagFate.Consumed, DrXmlParser.ClassifyTag("B"));   // case-insensitive
    }

    [Fact]
    public void Paired_b_element_emits_no_UnknownTagEvent()
    {
        var events = Feed("<b>PROFILE HELP</b>\n");
        Assert.Empty(events.OfType<UnknownTagEvent>());
    }

    [Fact]
    public void Paired_b_element_bolds_its_text_and_keeps_it()
    {
        var events = Feed("<b>PROFILE HELP</b>\n");
        var text   = events.OfType<TextEvent>().Single(e => e.Text.Contains("PROFILE HELP"));

        var span = Assert.Single(text.BoldSpans ?? Array.Empty<BoldSpan>());
        Assert.Equal("PROFILE HELP", text.Text.Substring(span.Start, span.Length));
    }

    [Fact]
    public void Bold_covers_only_the_b_span_within_a_longer_line()
    {
        var events = Feed("See <b>PROFILE</b> for details\n");
        var text   = events.OfType<TextEvent>().Single(e => e.Text.Contains("PROFILE"));

        var span = Assert.Single(text.BoldSpans ?? Array.Empty<BoldSpan>());
        Assert.Equal("PROFILE", text.Text.Substring(span.Start, span.Length));
    }

    [Fact]
    public void Stray_self_closing_b_is_ignored_and_leaves_no_dangling_span()
    {
        // A self-closing <b/> wraps no text; it must not push an unclosed entry
        // that mis-bolds the following pushBold span.
        var events = Feed("<b/>ready <pushBold/>item<popBold/>\n");
        var text   = events.OfType<TextEvent>().Single(e => e.Text.Contains("ready"));

        var span = Assert.Single(text.BoldSpans ?? Array.Empty<BoldSpan>());
        Assert.Equal("item", text.Text.Substring(span.Start, span.Length));
    }
}

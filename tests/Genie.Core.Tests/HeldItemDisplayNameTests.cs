using System;
using System.Collections.Generic;
using System.Linq;
using Genie.Core.Events;
using Genie.Core.Parser;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Public #172 — <c>$righthand</c> must carry the FULL display name from the
/// &lt;left&gt;/&lt;right&gt; body text ("whiskey jug"), not the noun attribute
/// ("jug") that <c>$righthandnoun</c> already exposes. The parser now emits
/// <see cref="HeldItemEvent"/> at the CLOSE tag with the body captured as
/// <c>Display</c>; the merge-seam recovery (appended game text with no
/// separator) must keep working alongside it.
/// </summary>
public class HeldItemDisplayNameTests
{
    private static List<GameEvent> Feed(params string[] chunks)
    {
        var parser = new DrXmlParser(NullLogger<DrXmlParser>.Instance);
        var events = new List<GameEvent>();
        using var _ = parser.GameEvents.Subscribe(new Collector(events));
        foreach (var chunk in chunks) parser.Feed(chunk);
        return events;
    }

    private sealed class Collector : IObserver<GameEvent>
    {
        private readonly List<GameEvent> _sink;
        public Collector(List<GameEvent> sink) => _sink = sink;
        public void OnNext(GameEvent e) => _sink.Add(e);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    [Fact]
    public void Body_text_becomes_display_name_noun_stays_noun()
    {
        // The issue's exact scenario: a held whiskey jug.
        var events = Feed("<right exist=\"35198906\" noun=\"jug\">whiskey jug</right>\n");

        var held = events.OfType<HeldItemEvent>().Single();
        Assert.Equal(Hand.Right,   held.Hand);
        Assert.Equal("jug",        held.Noun);
        Assert.Equal("35198906",   held.ExistId);
        Assert.Equal("whiskey jug", held.Display);
    }

    [Fact]
    public void Empty_hand_reports_Empty_display_with_blank_noun()
    {
        var events = Feed("<left>Empty</left>\n");

        var held = events.OfType<HeldItemEvent>().Single();
        Assert.Equal(Hand.Left, held.Hand);
        Assert.Equal("",        held.Noun);
        Assert.Equal("Empty",   held.Display);
    }

    [Fact]
    public void Merge_seam_still_splits_appended_game_text_from_the_display_name()
    {
        // The Kzin merge-seam case: the server concatenates a response onto
        // the hand body with no separator. The prefix is now KEPT as the
        // display name; the suffix must still be re-emitted as game text.
        var events = Feed(
            "<right exist=\"123\" noun=\"ledger\">black ledgerYou unlock and open your pack.</right>\n");

        var held = events.OfType<HeldItemEvent>().Single();
        Assert.Equal("ledger",       held.Noun);
        Assert.Equal("black ledger", held.Display);

        Assert.Contains(events.OfType<TextEvent>(),
                        t => t.Text.Contains("You unlock and open your pack."));
    }

    [Fact]
    public void Body_split_across_feed_chunks_still_assembles_the_display_name()
    {
        // The TCP read loop can split a tag's body anywhere.
        var events = Feed("<right exist=\"9\" noun=\"scimitar\">razor-edged ",
                          "scimitar</right>\n");

        var held = events.OfType<HeldItemEvent>().Single();
        Assert.Equal("razor-edged scimitar", held.Display);
        Assert.Equal("scimitar",             held.Noun);
    }

    [Fact]
    public void Self_closing_hand_tag_still_emits_from_attributes()
    {
        // Defensive: DR always sends the body form, but a self-closing tag
        // must not leave the parser stuck swallowing text into the hand buffer.
        var events = Feed("<left exist=\"7\" noun=\"shield\"/>\n",
                          "You see nothing unusual.\n");

        var held = events.OfType<HeldItemEvent>().Single();
        Assert.Equal("shield", held.Noun);
        Assert.Equal("",       held.Display);

        Assert.Contains(events.OfType<TextEvent>(),
                        t => t.Text.Contains("You see nothing unusual."));
    }

    [Fact]
    public void Both_hands_in_one_line_keep_their_own_attributes()
    {
        // Hands arrive adjacent on one raw line at every swap — the stashed
        // attributes must not bleed between the two elements.
        var events = Feed(
            "<right exist=\"1\" noun=\"scimitar\">razor-edged scimitar</right>" +
            "<left exist=\"2\" noun=\"shield\">tower shield</left>\n");

        var held = events.OfType<HeldItemEvent>().ToList();
        Assert.Equal(2, held.Count);
        Assert.Equal(("scimitar", "razor-edged scimitar", Hand.Right),
                     (held[0].Noun, held[0].Display, held[0].Hand));
        Assert.Equal(("shield", "tower shield", Hand.Left),
                     (held[1].Noun, held[1].Display, held[1].Hand));
    }
}

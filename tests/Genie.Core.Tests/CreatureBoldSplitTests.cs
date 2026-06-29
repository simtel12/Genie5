using System;
using System.Collections.Generic;
using System.Linq;
using Genie.Core.Events;
using Genie.Core.Parser;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Public issue #118 — same-type creatures joined by " and " with no comma
/// (the final pair of any DR list, and the only separator in a two-mob list)
/// were collapsed: the comma/period phrase scan in <c>ExtractBoldPhrases</c>
/// ran past " and " and swallowed the following creature, so "a giant viper and
/// a giant viper" became one entry and the monster count was wrong. The fix caps
/// each bold phrase at the next bold creature. These cases are verbatim
/// <c>room objs</c> components from recorded sessions (test_results/*.xml).
/// </summary>
public class CreatureBoldSplitTests
{
    private static IReadOnlyList<string> BoldNames(string roomObjsInner)
    {
        var parser = new DrXmlParser(NullLogger<DrXmlParser>.Instance);
        IReadOnlyList<string>? names = null;
        using var _ = parser.GameEvents.Subscribe(new Collector(e =>
        {
            if (e is ComponentEvent c && c.ComponentId == "room objs" && c.BoldNames is not null)
                names = c.BoldNames;
        }));
        parser.Feed($"<component id='room objs'>{roomObjsInner}</component>");
        return names ?? Array.Empty<string>();
    }

    private sealed class Collector : IObserver<GameEvent>
    {
        private readonly Action<GameEvent> _on;
        public Collector(Action<GameEvent> on) => _on = on;
        public void OnNext(GameEvent e) => _on(e);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    [Fact]
    public void TwoSameType_JoinedByAnd_AreSplit()
    {
        var names = BoldNames(
            "You also see <pushBold/>a giant viper<popBold/> and <pushBold/>a giant viper<popBold/>.");
        Assert.Equal(new[] { "a giant viper", "a giant viper" }, names);
    }

    [Fact]
    public void TwoDifferentType_JoinedByAnd_AreSplit()
    {
        // Verbatim from raw_session_*.xml.
        var names = BoldNames(
            "You also see <pushBold/>a town guard<popBold/> and <pushBold/>a grizzled old war veteran<popBold/>.");
        Assert.Equal(new[] { "a town guard", "a grizzled old war veteran" }, names);
    }

    [Fact]
    public void SingleCreature_AmongScenery_IsClean()
    {
        // Only the bold creature is a mob; scenery after the comma is not bold
        // and must not leak into the phrase.
        var names = BoldNames(
            "You also see <pushBold/>a town guard<popBold/>, the Crossing Forging Society Building, an iron anvil and a glowing forge.");
        Assert.Equal(new[] { "a town guard" }, names);
    }

    [Fact]
    public void ThreeSameType_MixedCommaAndAnd_AreAllSplit()
    {
        // Comma-separated then the final " and " pair — verbatim shape from a
        // sleazy-lout recording (scenery interleaved, three bold louts).
        var names = BoldNames(
            "You also see <pushBold/>a sleazy lout<popBold/>, a tumble-down mud brick hut, a crumbling mud brick house, <pushBold/>a sleazy lout<popBold/> and <pushBold/>a sleazy lout<popBold/>.");
        Assert.Equal(new[] { "a sleazy lout", "a sleazy lout", "a sleazy lout" }, names);
    }

    [Fact]
    public void TrailingDescriptor_IsPreservedOnLastCreature()
    {
        // The ignore-list relies on the descriptor surviving ("appears dead").
        var names = BoldNames(
            "You also see <pushBold/>a sleazy lout<popBold/> and <pushBold/>a sleazy lout<popBold/> that is trying to remain hidden.");
        Assert.Equal(new[] { "a sleazy lout", "a sleazy lout that is trying to remain hidden" }, names);
    }

    [Fact]
    public void TrailingDescriptor_IsPreservedBeforeAnd()
    {
        // Descriptor on a NON-final creature joined by " and " with no comma:
        // it sits before the next bold span, so the cap keeps it.
        var names = BoldNames(
            "You also see <pushBold/>a kobold<popBold/> that appears dead and <pushBold/>a giant rat<popBold/>.");
        Assert.Equal(new[] { "a kobold that appears dead", "a giant rat" }, names);
    }

    [Fact]
    public void NoCreatures_YieldsNothing()
    {
        var names = BoldNames("You also see a wooden sign and a glowing forge.");
        Assert.Empty(names);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Genie.Core.Events;
using Genie.Core.Parser;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Public issues #126/#127 — a Lich detachable attach happens after Lich
/// performed the login, so the login block (the only unprompted source of
/// room components and character identity) never reaches Genie. The parser
/// reconstructs both: an armed room-seed capture folds the next `look`
/// response into synthetic room ComponentEvents, and an armed ident window
/// consumes the `,eq respond "GENIE5-IDENT " + XMLData.name` reply into a
/// CharacterNameEvent. Raw formats verbatim from live captures
/// (raw_session_20260521_202149.xml).
/// </summary>
public class LichSeedTests
{
    private static (DrXmlParser Parser, List<GameEvent> Events) MakeParser()
    {
        var parser = new DrXmlParser(NullLogger<DrXmlParser>.Instance);
        var events = new List<GameEvent>();
        parser.GameEvents.Subscribe(new Collector(events));
        return (parser, events);
    }

    private sealed class Collector : IObserver<GameEvent>
    {
        private readonly List<GameEvent> _sink;
        public Collector(List<GameEvent> sink) => _sink = sink;
        public void OnNext(GameEvent e) => _sink.Add(e);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    // A `look` response as DR actually frames it (from the live recording):
    // roomName style toggle closes on the line AFTER the title; the objs text
    // rides the same raw line as </preset>; exits carry <d> link tags.
    private const string LookResponse =
        "<output class=\"\"/><resource picture=\"0\"/><style id=\"roomName\" />[Empaths' Guild, Courtyard Garden]\n" +
        "<style id=\"\"/><preset id='roomDesc'>Open to the sky, this spot is sheltered from the worst of the weather.</preset>  " +
        "You also see a rose, <pushBold/>a brown lynx<popBold/> that is sleeping and some junk.\n" +
        "Also here: Avoxies who is sitting and Madame Ivre.\n" +
        "Obvious paths: <d>northeast</d>, <d>east</d>.\n" +
        "<compass><dir value=\"ne\"/><dir value=\"e\"/></compass>[You are standing up.]\n";

    private static List<ComponentEvent> RoomComponents(IEnumerable<GameEvent> events) =>
        events.OfType<ComponentEvent>()
              .Where(c => c.ComponentId.StartsWith("room", StringComparison.Ordinal))
              .ToList();

    [Fact]
    public void ArmedSeed_SynthesizesAllFiveRoomComponents()
    {
        var (parser, events) = MakeParser();
        parser.BeginRoomSeedCapture();
        parser.Feed(LookResponse);

        var comps = RoomComponents(events).ToDictionary(c => c.ComponentId);

        Assert.Equal("[Empaths' Guild, Courtyard Garden]", comps["room title"].Content);
        Assert.Equal("Open to the sky, this spot is sheltered from the worst of the weather.",
                     comps["room desc"].Content);
        Assert.StartsWith("You also see a rose", comps["room objs"].Content);
        Assert.Equal("Also here: Avoxies who is sitting and Madame Ivre.",
                     comps["room players"].Content);
        Assert.Equal("Obvious paths: northeast, east.", comps["room exits"].Content);
    }

    [Fact]
    public void ArmedSeed_ObjsCarriesCreatureBold()
    {
        var (parser, events) = MakeParser();
        parser.BeginRoomSeedCapture();
        parser.Feed(LookResponse);

        var objs = RoomComponents(events).Single(c => c.ComponentId == "room objs");
        Assert.NotNull(objs.BoldNames);
        Assert.Contains("a brown lynx", objs.BoldNames!);
        // Spans index into the component content, same as the real component.
        var span = Assert.Single(objs.BoldSpans!);
        Assert.Equal("a brown lynx", objs.Content.Substring(span.Start, span.Length));
    }

    [Fact]
    public void UnarmedParser_SynthesizesNothing()
    {
        var (parser, events) = MakeParser();
        parser.Feed(LookResponse);

        Assert.Empty(RoomComponents(events));
    }

    [Fact]
    public void Seed_IsOneShot_SecondLookSynthesizesNothing()
    {
        var (parser, events) = MakeParser();
        parser.BeginRoomSeedCapture();
        parser.Feed(LookResponse);

        var afterFirst = RoomComponents(events).Count;
        parser.Feed(LookResponse);

        Assert.Equal(afterFirst, RoomComponents(events).Count);
    }

    [Fact]
    public void SeedLines_StillEmitAsTextEvents()
    {
        // The capture is a tee, not a suppressor — the look output must still
        // display in the game window exactly as before.
        var (parser, events) = MakeParser();
        parser.BeginRoomSeedCapture();
        parser.Feed(LookResponse);

        var text = events.OfType<TextEvent>().Select(t => t.Text).ToList();
        Assert.Contains(text, t => t.Contains("[Empaths' Guild, Courtyard Garden]"));
        Assert.Contains(text, t => t.Contains("Obvious paths"));
    }

    [Fact]
    public void RealRoomComponent_DisarmsTheSeed()
    {
        // Movement raced the seed: genuine components must win, and the stale
        // look text that follows must not overwrite them.
        var (parser, events) = MakeParser();
        parser.BeginRoomSeedCapture();
        parser.Feed("<component id='room desc'>The real description.</component>");
        parser.Feed(LookResponse);

        var descs = RoomComponents(events).Where(c => c.ComponentId == "room desc").ToList();
        Assert.Single(descs);
        Assert.Equal("The real description.", descs[0].Content);
        Assert.DoesNotContain(RoomComponents(events), c => c.ComponentId == "room title");
    }

    [Fact]
    public void Nav_DisarmsTheSeed()
    {
        var (parser, events) = MakeParser();
        parser.BeginRoomSeedCapture();
        parser.Feed("<nav/>");
        parser.Feed(LookResponse);

        Assert.Empty(RoomComponents(events));
    }

    // ── Ident reply (#127) ───────────────────────────────────────────────────

    // Lich's respond() to a mono-capable frontend brackets the payload:
    private const string IdentReply =
        "<output class=\"mono\"/>\r\nGENIE5-IDENT Renucci\r\n<output class=\"\"/>\r\n";

    [Fact]
    public void ArmedIdent_EmitsCharacterName_AndSwallowsTheMarkerLine()
    {
        var (parser, events) = MakeParser();
        parser.BeginLichIdentWindow();
        parser.Feed(IdentReply);

        var name = Assert.Single(events.OfType<CharacterNameEvent>());
        Assert.Equal("Renucci", name.Name);
        Assert.DoesNotContain(events.OfType<TextEvent>(), t => t.Text.Contains("GENIE5-IDENT"));
    }

    [Fact]
    public void ArmedIdent_IsOneShot()
    {
        var (parser, events) = MakeParser();
        parser.BeginLichIdentWindow();
        parser.Feed(IdentReply);
        parser.Feed("<output class=\"mono\"/>\r\nGENIE5-IDENT Someone\r\n<output class=\"\"/>\r\n");

        Assert.Single(events.OfType<CharacterNameEvent>());
        // The second marker line is ordinary text once the window is closed.
        Assert.Contains(events.OfType<TextEvent>(), t => t.Text.Contains("GENIE5-IDENT Someone"));
    }

    [Fact]
    public void UnarmedIdent_LineDisplaysNormally_NoEvent()
    {
        var (parser, events) = MakeParser();
        parser.Feed(IdentReply);

        Assert.Empty(events.OfType<CharacterNameEvent>());
        Assert.Contains(events.OfType<TextEvent>(), t => t.Text.Contains("GENIE5-IDENT Renucci"));
    }

    [Fact]
    public void CharacterNameEvent_UpdatesGameState()
    {
        var (parser, _) = MakeParser();
        var state  = new Genie.Core.Models.GameState();
        using var engine = new Genie.Core.GameState.GameStateEngine(
            parser.GameEvents, state,
            NullLogger<Genie.Core.GameState.GameStateEngine>.Instance);

        parser.BeginLichIdentWindow();
        parser.Feed(IdentReply);

        Assert.Equal("Renucci", state.CharacterName);
    }
}

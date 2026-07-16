using System;
using System.Collections.Generic;
using System.Linq;
using Genie.Core.Events;
using Genie.Core.Parser;
using Genie.Core.Scripting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// The login stream's <c>&lt;app char=… game=… title=…/&gt;</c> tag is the
/// server's authoritative session identity (Genie 4 Core/Game.cs:1903). It was
/// in the settings skip-list, so <c>$game</c> stayed at the connect dialog's
/// construction-time guess forever — in a Lich session that guess is usually
/// "DR", which silently disables every Platinum-portal / Fallen-shortcut branch
/// community travel scripts gate on <c>matchre("$game","(?i)DRX|DRF")</c>.
/// The tag now emits AppEvent, and ScriptGlobalsSync corrects
/// <c>$game</c>/<c>$gamename</c>/<c>$charactername</c> from it. The
/// settings-dump form <c>&lt;app maximized='t'/&gt;</c> (no char) stays inert.
/// </summary>
public class AppEventTests
{
    // ── Parser ────────────────────────────────────────────────────────────
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
    public void App_is_classified_as_consumed()
    {
        Assert.Equal(DrXmlParser.TagFate.Consumed, DrXmlParser.ClassifyTag("app"));
    }

    [Fact]
    public void App_tag_with_identity_emits_AppEvent()
    {
        var events = Feed("<app char=\"Renucci\" game=\"DR\" title=\"[DR: Renucci] Wrayth\"/>\n");
        var app = Assert.Single(events.OfType<AppEvent>());
        Assert.Equal("Renucci", app.Character);
        Assert.Equal("DR", app.Game);
        Assert.Equal("[DR: Renucci] Wrayth", app.Title);
    }

    [Fact]
    public void App_tag_reports_the_platinum_instance()
    {
        var events = Feed("<app char=\"Shroom\" game=\"DRX\" title=\"[DRX: Shroom]\"/>\n");
        Assert.Equal("DRX", Assert.Single(events.OfType<AppEvent>()).Game);
    }

    [Fact]
    public void Settings_dump_app_form_stays_inert()
    {
        var events = Feed("<app maximized='t'/>\n");
        Assert.Empty(events.OfType<AppEvent>());
        Assert.Empty(events.OfType<UnknownTagEvent>());
    }

    // ── Globals sync ──────────────────────────────────────────────────────
    private sealed class ManualEvents : IObservable<GameEvent>
    {
        private readonly List<IObserver<GameEvent>> _observers = new();
        public IDisposable Subscribe(IObserver<GameEvent> observer)
        { _observers.Add(observer); return new Nop(); }
        public void OnNext(GameEvent e)
        { foreach (var o in _observers.ToArray()) o.OnNext(e); }
        private sealed class Nop : IDisposable { public void Dispose() { } }
    }

    private static (Dictionary<string, string> globals, ManualEvents events, ScriptGlobalsSync sync)
        MakeSync(string seedGameCode)
    {
        var globals = new Dictionary<string, string>();
        var events  = new ManualEvents();
        var sync    = new ScriptGlobalsSync(
            new Genie.Core.Models.GameState(), globals, events,
            gameCode: seedGameCode, characterName: "Placeholder");
        return (globals, events, sync);
    }

    [Fact]
    public void AppEvent_corrects_game_gamename_and_charactername()
    {
        // The Lich scenario: dialog guessed Prime, Lich is logged into The Fallen.
        var (globals, events, sync) = MakeSync("DR");
        using var _ = sync;
        Assert.Equal("DR", globals["game"]);

        events.OnNext(new AppEvent("Renucci", "DRF", "[DRF: Renucci]"));

        Assert.Equal("DRF",     globals["game"]);
        Assert.Equal("DRF",     globals["gamename"]);
        Assert.Equal("Renucci", globals["charactername"]);
    }

    [Fact]
    public void AppEvent_game_value_is_normalized_g4_style()
    {
        // Genie 4 strips ':' and spaces from the attr (Core/Game.cs:1922).
        var (globals, events, sync) = MakeSync("DR");
        using var _ = sync;

        events.OnNext(new AppEvent("Renucci", "DR: The Fallen", ""));

        Assert.Equal("DRTheFallen", globals["game"]);
        Assert.Equal("DRTheFallen", globals["gamename"]);
    }

    [Fact]
    public void AppEvent_with_blank_game_keeps_the_seed()
    {
        var (globals, events, sync) = MakeSync("DRX");
        using var _ = sync;

        events.OnNext(new AppEvent("Renucci", "", ""));

        Assert.Equal("DRX",     globals["game"]);      // seed untouched
        Assert.Equal("Renucci", globals["charactername"]);
    }
}

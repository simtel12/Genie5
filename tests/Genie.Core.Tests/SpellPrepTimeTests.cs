using System;
using System.Collections.Generic;
using Genie.Core.Events;
using Genie.Core.Models;
using Genie.Core.Scripting;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// <c>$spellpreptime</c> — Genie 4 semantics (Globals.cs ParseSpecialVariables):
/// <c>@spellpreptime@</c> = casttime − spellstarttime, the spell's FULL prep
/// length in seconds (constant per spell), NOT the elapsed count-up (that is
/// <c>$spelltime</c>). The first cut of this variable mirrored $spelltime —
/// this locks the corrected parity.
/// </summary>
public class SpellPrepTimeTests
{
    private sealed class Feed : IObservable<GameEvent>
    {
        private readonly List<IObserver<GameEvent>> _subs = new();
        public IDisposable Subscribe(IObserver<GameEvent> observer)
        {
            _subs.Add(observer);
            return new Unsub(() => _subs.Remove(observer));
        }
        public void Push(GameEvent e) { foreach (var s in _subs.ToArray()) s.OnNext(e); }
        private sealed class Unsub : IDisposable
        {
            private readonly Action _a;
            public Unsub(Action a) => _a = a;
            public void Dispose() => _a();
        }
    }

    private static (Genie.Core.Models.GameState state, Dictionary<string, string> globals, Feed feed) Fixture()
    {
        var state   = new Genie.Core.Models.GameState();
        var globals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var feed    = new Feed();
        _ = new ScriptGlobalsSync(state, globals, feed);
        return (state, globals, feed);
    }

    [Fact]
    public void Spellpreptime_is_the_full_prep_length_not_the_elapsed_countup()
    {
        var (state, globals, feed) = Fixture();

        // Prep began 10s ago; the cast will be fully prepared 68s from now →
        // total prep length 78s. The count-up ($spelltime) would say ~10.
        var now = DateTimeOffset.UtcNow;
        state.Combat.SpellTimeStart = now.AddSeconds(-10);
        state.Combat.CastTimeEnd    = now.AddSeconds(68);

        feed.Push(new PromptEvent(now));

        Assert.Equal("78", globals["spellpreptime"]);
    }

    [Fact]
    public void Spellpreptime_is_zero_with_no_spell_prepared()
    {
        var (state, globals, feed) = Fixture();

        state.Combat.SpellTimeStart = null;
        state.Combat.CastTimeEnd    = DateTimeOffset.UtcNow.AddSeconds(30);

        feed.Push(new PromptEvent(DateTimeOffset.UtcNow));

        Assert.Equal("0", globals["spellpreptime"]);
    }

    [Fact]
    public void Spellpreptime_is_zero_when_the_server_sent_no_casttime()
    {
        var (state, globals, feed) = Fixture();

        state.Combat.SpellTimeStart = DateTimeOffset.UtcNow.AddSeconds(-5);
        state.Combat.CastTimeEnd    = default;     // never reported

        feed.Push(new PromptEvent(DateTimeOffset.UtcNow));

        Assert.Equal("0", globals["spellpreptime"]);
    }
}

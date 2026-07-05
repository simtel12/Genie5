using System;
using Genie.Core.Config;
using Genie.Core.Events;
using Genie.Core.Parser;
using Genie.Core.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// The monster-count ignore list (<c>monstercountignorelist</c>) and its
/// Mobs-panel editor plumbing: the top-level-alternative splitter that renders
/// the pipe-joined regex one row at a time, the ConfigChanged(MonsterIgnore)
/// notify that makes edits (UI or typed <c>#config</c>) apply live, and
/// GameStateEngine.RecomputeCreatures — re-filtering Room.Creatures against
/// the new list without waiting for the next <c>room objs</c> event.
/// </summary>
public class MonsterIgnoreListTests
{
    // ── SplitTopLevelAlternatives ────────────────────────────────────────

    [Fact]
    public void Split_DefaultList_YieldsBothAlternatives()
    {
        Assert.Equal(new[] { "appears dead", "(dead)" },
            GenieConfig.SplitTopLevelAlternatives(GenieConfig.DefaultIgnoreMonsterList));
    }

    [Fact]
    public void Split_PipeInsideGroup_IsNotABoundary()
    {
        Assert.Equal(new[] { "(rat|hog)", "cat" },
            GenieConfig.SplitTopLevelAlternatives("(rat|hog)|cat"));
    }

    [Fact]
    public void Split_PipeInsideCharacterClass_IsNotABoundary()
    {
        Assert.Equal(new[] { "[a|b]x", "cat" },
            GenieConfig.SplitTopLevelAlternatives("[a|b]x|cat"));
    }

    [Fact]
    public void Split_EscapedPipe_IsNotABoundary()
    {
        Assert.Equal(new[] { @"a\|b", "cat" },
            GenieConfig.SplitTopLevelAlternatives(@"a\|b|cat"));
    }

    [Fact]
    public void Split_EmptyAlternatives_AreDropped()
    {
        Assert.Equal(new[] { "a", "b" }, GenieConfig.SplitTopLevelAlternatives("a||b|"));
        Assert.Empty(GenieConfig.SplitTopLevelAlternatives(""));
        Assert.Empty(GenieConfig.SplitTopLevelAlternatives("   "));
    }

    [Fact]
    public void Split_JoinRoundTrip_IsLossless()
    {
        const string list = @"appears dead|(dead)|a\ furry\ lynx|(rat|hog)";
        Assert.Equal(list, string.Join("|", GenieConfig.SplitTopLevelAlternatives(list)));
    }

    // ── ConfigChanged notify ─────────────────────────────────────────────

    [Fact]
    public void SetSetting_IgnoreList_FiresMonsterIgnore()
    {
        var cfg = new GenieConfig(new LocalDirectoryService("Genie5Test", AppContext.BaseDirectory));
        ConfigFieldUpdated? fired = null;
        cfg.ConfigChanged += f => fired = f;

        cfg.SetSetting("monstercountignorelist", "appears dead|snow lynx");

        Assert.Equal(ConfigFieldUpdated.MonsterIgnore, fired);
        Assert.Equal("appears dead|snow lynx", cfg.IgnoreMonsterList);
    }

    // ── RecomputeCreatures ───────────────────────────────────────────────

    private const string RoomObjs =
        "<component id='room objs'>You also see <pushBold/>a musk hog<popBold/> and " +
        "<pushBold/>a furry-footed snow lynx that is sitting<popBold/>.</component>";

    private static (DrXmlParser Parser, Genie.Core.GameState.GameStateEngine Engine,
                    Genie.Core.Models.GameState State, GenieConfig Config) MakeStack()
    {
        var parser = new DrXmlParser(NullLogger<DrXmlParser>.Instance);
        var state  = new Genie.Core.Models.GameState();
        var engine = new Genie.Core.GameState.GameStateEngine(
            parser.GameEvents, state,
            NullLogger<Genie.Core.GameState.GameStateEngine>.Instance);
        var cfg = new GenieConfig(new LocalDirectoryService("Genie5Test", AppContext.BaseDirectory));
        engine.Config = cfg;
        return (parser, engine, state, cfg);
    }

    [Fact]
    public void Recompute_AppliesNewIgnoreList_WithoutNewRoomEvent()
    {
        var (parser, engine, state, cfg) = MakeStack();
        using var _ = engine;

        parser.Feed(RoomObjs);
        Assert.Equal(2, state.Room.MonsterCount);   // nothing ignored yet

        cfg.SetSetting("monstercountignorelist", "appears dead|(dead)|snow lynx");
        engine.RecomputeCreatures();

        Assert.Equal(new[] { "a musk hog" }, state.Room.Creatures);
        Assert.Equal(1, state.Room.MonsterCount);
    }

    [Fact]
    public void Recompute_RemovedPattern_RestoresCreature()
    {
        var (parser, engine, state, cfg) = MakeStack();
        using var _ = engine;

        cfg.SetSetting("monstercountignorelist", "snow lynx");
        parser.Feed(RoomObjs);
        Assert.Equal(new[] { "a musk hog" }, state.Room.Creatures);

        cfg.SetSetting("monstercountignorelist", "");
        engine.RecomputeCreatures();

        Assert.Equal(2, state.Room.MonsterCount);   // lynx is back
    }

    [Fact]
    public void Recompute_BeforeAnyRoomEvent_LeavesStateUntouched()
    {
        var (_, engine, state, cfg) = MakeStack();
        using var _ = engine;

        state.Room.Creatures    = new[] { "carried across reconnect" };
        state.Room.MonsterCount = 1;

        cfg.SetSetting("monstercountignorelist", "anything");
        engine.RecomputeCreatures();   // no room objs seen → must not wipe

        Assert.Equal(new[] { "carried across reconnect" }, state.Room.Creatures);
        Assert.Equal(1, state.Room.MonsterCount);
    }

    [Fact]
    public void Recompute_EscapedExactPhrase_MatchesRightClickIgnore()
    {
        var (parser, engine, state, cfg) = MakeStack();
        using var _ = engine;

        parser.Feed(RoomObjs);
        // What the Mobs panel's right-click → Ignore writes: the exact phrase,
        // regex-escaped, appended as one more alternative.
        var escaped = System.Text.RegularExpressions.Regex.Escape(
            "a furry-footed snow lynx that is sitting");
        cfg.SetSetting("monstercountignorelist",
            GenieConfig.DefaultIgnoreMonsterList + "|" + escaped);
        engine.RecomputeCreatures();

        Assert.Equal(new[] { "a musk hog" }, state.Room.Creatures);
    }
}

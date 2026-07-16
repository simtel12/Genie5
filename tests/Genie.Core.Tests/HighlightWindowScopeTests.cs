using System;
using System.IO;
using System.Linq;
using Genie.Core.Highlights;
using Genie.Core.Persistence;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Per-window highlight scoping: a rule with an empty Windows set paints
/// everywhere (the default, so existing/Genie-4-imported rules are unaffected),
/// and a rule that lists windows paints only in those. Plus the highlights.json
/// round-trip of the new Windows field.
/// </summary>
public class HighlightWindowScopeTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "genie-hlwin-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Empty_window_set_applies_everywhere()
    {
        var rule = new HighlightRule("orc", "Red");
        Assert.Empty(rule.Windows);
        Assert.True(rule.AppliesToWindow("main"));
        Assert.True(rule.AppliesToWindow("room"));
        Assert.True(rule.AppliesToWindow("thoughts"));
        Assert.True(rule.AppliesToWindow("anything"));
    }

    [Fact]
    public void Listed_windows_restrict_where_the_rule_paints()
    {
        var rule = new HighlightRule("Obvious paths", "Cyan",
            windows: new[] { "room", "main" });
        Assert.True(rule.AppliesToWindow("room"));
        Assert.True(rule.AppliesToWindow("MAIN"));       // case-insensitive
        Assert.False(rule.AppliesToWindow("thoughts"));
        Assert.False(rule.AppliesToWindow("mobs"));
    }

    [Fact]
    public void Blank_and_whitespace_window_entries_are_dropped()
    {
        var rule = new HighlightRule("x", "Red", windows: new[] { "room", "  ", "", "mobs " });
        Assert.Equal(new[] { "mobs", "room" }, rule.Windows.OrderBy(w => w).ToArray());
        // A set that is all-blank collapses back to "everywhere".
        var empty = new HighlightRule("y", "Red", windows: new[] { " ", "" });
        Assert.Empty(empty.Windows);
        Assert.True(empty.AppliesToWindow("anywhere"));
    }

    [Fact]
    public void SetWindows_replaces_scope_and_empty_clears_it()
    {
        var rule = new HighlightRule("x", "Red", windows: new[] { "room" });
        rule.SetWindows(new[] { "mobs", "players" });
        Assert.False(rule.AppliesToWindow("room"));
        Assert.True(rule.AppliesToWindow("players"));
        rule.SetWindows(null);
        Assert.True(rule.AppliesToWindow("room"));       // back to everywhere
    }

    [Fact]
    public void Engine_add_rule_carries_the_window_scope()
    {
        var engine = new HighlightEngine();
        engine.AddRule("Kobold", "Yellow", windows: new[] { "mobs" });
        var rule = Assert.Single(engine.Rules);
        Assert.True(rule.AppliesToWindow("mobs"));
        Assert.False(rule.AppliesToWindow("main"));
    }

    [Fact]
    public void Windows_survive_the_highlights_json_round_trip()
    {
        var svc  = new PersistenceService();
        var path = Path.Combine(_dir, "highlights.json");

        var engine = new HighlightEngine();
        engine.AddRule("Obvious paths", "Cyan", windows: new[] { "room" });
        engine.AddRule("everywhere", "Red");             // empty scope
        svc.SaveHighlights(path, engine.Rules);

        var models = svc.LoadHighlights(path);
        Assert.Equal(2, models.Count);
        var scoped = models.Single(m => m.Pattern == "Obvious paths");
        Assert.Equal(new[] { "room" }, scoped.Windows);
        var global = models.Single(m => m.Pattern == "everywhere");
        Assert.Empty(global.Windows);
    }

    [Fact]
    public void Older_json_without_windows_loads_as_apply_everywhere()
    {
        var path = Path.Combine(_dir, "highlights.json");
        // A pre-feature file: no "Windows" key at all.
        File.WriteAllText(path,
            "[{\"Pattern\":\"orc\",\"ForegroundColor\":\"Red\",\"MatchType\":\"String\",\"IsEnabled\":true}]");

        var models = new PersistenceService().LoadHighlights(path);
        var m = Assert.Single(models);
        Assert.NotNull(m.Windows);
        Assert.Empty(m.Windows);                         // → HighlightRule paints everywhere
    }
}

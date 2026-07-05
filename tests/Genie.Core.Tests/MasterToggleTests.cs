using System;
using System.Collections.Generic;
using System.Linq;
using Genie.Core.Aliases;
using Genie.Core.Config;
using Genie.Core.Gags;
using Genie.Core.Highlights;
using Genie.Core.Runtime;
using Genie.Core.Substitutes;
using Genie.Core.Triggers;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Master enable toggles for the user rule engines (File ▸ Master Toggles /
/// <c>#config highlights|triggers|substitutes|gags|aliases</c> — issue #26
/// Genie 4 File-menu parity). Turning an engine off must stop it applying
/// while leaving its rules loaded and editable; the config keys persist and
/// fire ConfigChanged(MasterToggles) so the engines and menu stay in sync.
/// </summary>
public class MasterToggleTests
{
    // ── Engine gates ─────────────────────────────────────────────────────

    [Fact]
    public void Substitutes_disabled_returns_line_untouched_and_keeps_rules()
    {
        var engine = new SubstituteEngine();
        engine.AddRule("dragon", "DRAGON");
        Assert.Equal("a DRAGON appears", engine.Apply("a dragon appears"));

        engine.Enabled = false;
        Assert.Equal("a dragon appears", engine.Apply("a dragon appears"));
        Assert.Single(engine.Rules);   // rules survive the toggle

        engine.Enabled = true;
        Assert.Equal("a DRAGON appears", engine.Apply("a dragon appears"));
    }

    [Fact]
    public void Gags_disabled_never_gags_and_keeps_rules()
    {
        var engine = new GagEngine();
        engine.AddRule("swims north");
        Assert.True(engine.ShouldGag("A fish swims north."));

        engine.Enabled = false;
        Assert.False(engine.ShouldGag("A fish swims north."));
        Assert.Single(engine.Rules);

        engine.Enabled = true;
        Assert.True(engine.ShouldGag("A fish swims north."));
    }

    [Fact]
    public void Highlights_disabled_matches_nothing_and_keeps_rules()
    {
        var engine = new HighlightEngine();
        engine.AddRule("Renucci", "Red");
        Assert.NotNull(engine.Match("Renucci waves."));

        engine.Enabled = false;
        Assert.Null(engine.Match("Renucci waves."));
        Assert.Single(engine.Rules);

        engine.Enabled = true;
        Assert.NotNull(engine.Match("Renucci waves."));
    }

    [Fact]
    public void Aliases_disabled_pass_input_through_unexpanded()
    {
        var engine = new AliasEngine();   // null command engine — TryProcess result is the signal
        engine.AddAlias("hb", "hunt badger");
        Assert.True(engine.TryProcess("hb"));

        engine.Enabled = false;
        Assert.False(engine.TryProcess("hb"));
        Assert.Single(engine.Aliases);

        engine.Enabled = true;
        Assert.True(engine.TryProcess("hb"));
    }

    [Fact]
    public void Triggers_default_enabled_and_toggle_holds()
    {
        var engine = new TriggerEngineFinal();
        Assert.True(engine.Enabled);
        engine.AddTrigger("^You are stunned", "stand");

        // Disabled ProcessLine must be a quiet no-op (no host/command engine
        // wired here — the gate short-circuits before either is touched).
        engine.Enabled = false;
        engine.ProcessLine("You are stunned!");
        Assert.Single(engine.Triggers);
    }

    // ── Config keys ──────────────────────────────────────────────────────

    private static GenieConfig NewConfig() =>
        new(new LocalDirectoryService("Genie5Test", AppContext.BaseDirectory));

    [Fact]
    public void Config_keys_default_on_and_toggle_off()
    {
        var cfg = new GenieConfig(new LocalDirectoryService("Genie5Test", AppContext.BaseDirectory));
        Assert.True(cfg.EnableHighlights);
        Assert.True(cfg.EnableTriggers);
        Assert.True(cfg.EnableSubstitutes);
        Assert.True(cfg.EnableGags);
        Assert.True(cfg.EnableAliases);

        cfg.SetSetting("triggers", "off");
        cfg.SetSetting("gags", "false");
        Assert.False(cfg.EnableTriggers);
        Assert.False(cfg.EnableGags);
        Assert.True(cfg.EnableHighlights);   // untouched keys stay on
    }

    [Fact]
    public void Config_toggle_fires_MasterToggles_changed_event()
    {
        var cfg = NewConfig();
        var fired = new List<ConfigFieldUpdated>();
        cfg.ConfigChanged += f => fired.Add(f);

        cfg.SetSetting("aliases", "off");
        Assert.Contains(ConfigFieldUpdated.MasterToggles, fired);
    }

    [Fact]
    public void Images_key_defaults_on_toggles_off_and_fires_ImagesEnabled()
    {
        // File ▸ Master Toggles ▸ Images rides the pre-existing `showimages`
        // key, which fires ImagesEnabled (not MasterToggles) — the menu and
        // SceneViewModel both listen for it.
        var cfg = NewConfig();
        Assert.True(cfg.ShowImages);

        var fired = new List<ConfigFieldUpdated>();
        cfg.ConfigChanged += f => fired.Add(f);

        cfg.SetSetting("showimages", "off");
        Assert.False(cfg.ShowImages);
        Assert.Contains(ConfigFieldUpdated.ImagesEnabled, fired);
        Assert.Equal("False", cfg.GetSetting("showimages"));   // persists via ToConfigPairs
    }

    [Fact]
    public void Config_pairs_carry_all_five_keys_for_settings_cfg()
    {
        var cfg = NewConfig();
        var keys = cfg.ToConfigPairs().Select(p => p.Key).ToList();
        foreach (var key in new[] { "highlights", "triggers", "substitutes", "gags", "aliases" })
            Assert.Contains(key, keys);

        // GetSetting reads back the live value (backs `#config triggers`).
        cfg.SetSetting("substitutes", "off");
        Assert.Equal("False", cfg.GetSetting("substitutes"));
    }
}

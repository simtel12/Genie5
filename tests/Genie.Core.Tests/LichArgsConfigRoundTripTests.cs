using System;
using System.IO;
using System.Linq;
using Genie.Core.Commanding;
using Genie.Core.Config;
using Genie.Core.Connection;
using Genie.Core.Parsing;
using Genie.Core.Queue;
using Genie.Core.Runtime;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Nested-brace <c>#config lichargs {--login {character} …}</c> round-trip —
/// the path the PR review asked about. Existing <see cref="LichArgsExpandTests"/>
/// call <see cref="LichLauncher.TryExpandArguments"/> directly; these cover the
/// config parser / settings.cfg / <c>#ls</c> surface instead.
/// </summary>
public class LichArgsConfigRoundTripTests : IDisposable
{
    private const string NestedTemplate =
        "--login {character} --dragonrealms --genie --headless {port}";

    private const string NestedConfigLine =
        "#config lichargs {--login {character} --dragonrealms --genie --headless {port}}";

    private readonly string _root;
    private readonly GenieConfig _config;

    public LichArgsConfigRoundTripTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_lichargs_rt_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieLichArgsRt", _root);
        lds.UseExplicitRoot(_root);
        _config = new GenieConfig(lds);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    [Fact]
    public void ArgumentParser_strips_outer_braces_keeps_inner_placeholders()
    {
        // Mirrors what CommandEngine feeds HandleInternalCommand (command char stripped).
        var parts = ArgumentParser.ParseArgs(
            "config lichargs {--login {character} --dragonrealms --genie --headless {port}}");

        Assert.Equal(new[] { "config", "lichargs", NestedTemplate }, parts.ToArray());
    }

    [Fact]
    public void SettingsCfg_save_load_preserves_inner_placeholders()
    {
        _config.SetSetting("lichargs", NestedTemplate, showException: false);
        Assert.True(_config.Save());

        var onDisk = File.ReadAllText(Path.Combine(_config.ConfigDir, "settings.cfg"));
        Assert.Contains("{lichargs}", onDisk);
        Assert.Contains("{character}", onDisk);
        Assert.Contains("{port}", onDisk);
        // Saved line must wrap the value; inner placeholders stay inside that wrapper.
        Assert.Contains(
            "#config {lichargs} {--login {character} --dragonrealms --genie --headless {port}}",
            onDisk);

        // Clear then reload from the just-written settings.cfg (same ConfigDir).
        _config.LichArguments = "SHOULD_BE_OVERWRITTEN";
        Assert.True(_config.Load());

        Assert.Equal(NestedTemplate, _config.LichArguments);
        // Outer grouping braces must be stripped; inner {character}/{port} stay.
        Assert.False(_config.LichArguments.StartsWith("{--login", StringComparison.Ordinal));
        Assert.DoesNotContain("{{", _config.LichArguments); // no brace accumulation on re-save
    }

    [Fact]
    public void Typed_config_line_via_ParseArgs_then_SetSetting_stores_verbatim()
    {
        // CommandEngine path: ParseArgs → join Skip(1) → App Split(' ', 2) → SetSetting.
        var parts = ArgumentParser.ParseArgs(NestedConfigLine[1..]); // strip '#'
        var forwarded = string.Join(" ", parts.Skip(1));
        var split = forwarded.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("lichargs", split[0]);
        Assert.Equal(NestedTemplate, split[1]);

        _config.SetSetting(split[0], split[1], showException: false);
        Assert.Equal(NestedTemplate, _config.LichArguments);

        Assert.True(LichLauncher.TryExpandArguments(
            _config.LichArguments, "MyChar", 8000, out var expanded, out var error));
        Assert.Equal("--login MyChar --dragonrealms --genie --headless 8000", expanded);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void Ls_shows_stored_placeholders_after_nested_brace_set()
    {
        _config.SetSetting("lichargs", NestedTemplate, showException: false);
        var host = new CapturingHost();
        var engine = new CommandEngine(_config, new CommandQueue(), new EventQueue(), host);

        engine.ProcessInput("#ls");

        var dump = string.Join("\n", host.Echoes);
        Assert.Contains(NestedTemplate, dump);
        Assert.Contains("Placeholders:", dump);
        Assert.Contains("{character}", dump);
        Assert.Contains("{port}", dump);
    }

    [Fact]
    public void App_style_Split_without_ParseArgs_would_keep_outer_braces()
    {
        // Documents the latent App-layer footgun: HandleConfigCommand uses a
        // naive Split(' ', 2) and does not strip {…}. Today CommandEngine
        // ParseArgs's first, so the live typed path is safe — but if anything
        // ever forwards the braced remainder raw, outer braces would be stored.
        var rawForwarded = "lichargs {--login {character} --dragonrealms --genie --headless {port}}";
        var split = rawForwarded.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(
            "{--login {character} --dragonrealms --genie --headless {port}}",
            split[1]);
    }

    private sealed class CapturingHost : ICommandHost
    {
        public System.Collections.Generic.List<string> Echoes { get; } = new();
        public System.Collections.Generic.Dictionary<string, string> Globals { get; } = new();

        public void Echo(string text) => Echoes.Add(text);
        public System.Collections.Generic.IReadOnlyDictionary<string, string> GetGlobalVariables() => Globals;
        public string ExpandVariables(string text) => text;
        public void MapperReset() { }
        public void MapperCommand(string args) { }
        public void SendToGame(string text, bool userInput = false, string origin = "", string? echoOverride = null) { }
        public void EchoTo(string text, string? window, string? color) { }
        public void EchoMain(string text, string? color, bool mono) { }
        public void EchoLink(string text, string command, string? window) { }
        public void EchoClear(string? window) { }
        public void WindowCommand(string sub, string window) { }
        public void SetStatusBar(string text, int index) { }
        public void RunScript(string text) { }
        public void InjectParsedLine(string line) { }
        public void StopScript(string? name) { }
        public void PauseScript(string? name) { }
        public void ResumeScript(string? name) { }
        public void StopAllScripts() { }
        public void PauseAllScripts() { }
        public void ResumeAllScripts() { }
        public void SetTraceLevelAll(int level) { }
        public System.Collections.Generic.IReadOnlyList<string> RunningScripts() => Array.Empty<string>();
        public void SetGlobalVariable(string name, string value) => Globals[name] = value;
        public void RemoveGlobalVariable(string name) => Globals.Remove(name);
        public string SetLiveAudit(Genie.Core.Diagnostics.AuditMode mode) => string.Empty;
        public void EditScript(string name) { }
        public void LayoutCommand(string args) { }
        public void PluginCommand(string args) { }
        public void ConfigCommand(string args) { }
        public void MapperGoto(string args) { }
        public void PlaySound(string soundName) { }
        public void Speak(string text, bool urgent = false) { }
        public void TtsCommand(string args) { }
        public void FlashWindow() { }
        public void Connect(ConnectRequest request) { }
    }
}

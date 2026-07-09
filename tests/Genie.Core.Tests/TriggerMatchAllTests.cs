using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genie.Core.Commanding;
using Genie.Core.Config;
using Genie.Core.Queue;
using Genie.Core.Runtime;
using Genie.Core.Triggers;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// #23 — opt-in per-rule "match all" on triggers. When set, the action fires once
/// per match on the line (each with its own $0..$n) instead of once for the first
/// match. Default off preserves the fire-once-per-line behaviour. (Highlight and
/// Substitute already act on every match natively.)
/// </summary>
public class TriggerMatchAllTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;

    public TriggerMatchAllTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_matchall_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieMatchAllTest", _root);
        lds.UseExplicitRoot(_root);
        _config = new GenieConfig(lds);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    private (FakeCommandHost host, CommandEngine cmd, TriggerEngineFinal trig) Make()
    {
        var host = new FakeCommandHost();
        var cmd  = new CommandEngine(_config, new CommandQueue(), new EventQueue(), host);
        var trig = new TriggerEngineFinal(host, cmd);
        cmd.Triggers = trig;
        return (host, cmd, trig);
    }

    [Fact]
    public void MatchAll_fires_the_action_once_per_match()
    {
        var (host, _, trig) = Make();
        trig.AddTrigger(@"(\w+) arrives", "stare $1", matchAll: true);
        trig.ProcessLine("Bob arrives. Al arrives.");

        Assert.Equal(new[] { "stare Bob", "stare Al" }, host.GameCommands);
    }

    [Fact]
    public void Default_fires_once_with_the_first_match()
    {
        var (host, _, trig) = Make();
        trig.AddTrigger(@"(\w+) arrives", "stare $1");   // matchAll off
        trig.ProcessLine("Bob arrives. Al arrives.");

        Assert.Equal(new[] { "stare Bob" }, host.GameCommands);
    }

    [Fact]
    public void MatchAll_with_no_match_fires_nothing()
    {
        var (host, _, trig) = Make();
        trig.AddTrigger(@"(\w+) arrives", "stare $1", matchAll: true);
        trig.ProcessLine("nothing here");

        Assert.Empty(host.GameCommands);
    }

    [Fact]
    public void MatchAll_sound_fires_once_per_line_not_per_match()
    {
        var (host, _, trig) = Make();
        trig.AddTrigger(@"(\w+) arrives", "stare $1", matchAll: true, soundFile: "ding.wav");
        trig.ProcessLine("Bob arrives. Al arrives.");

        Assert.Equal(2, host.GameCommands.Count);   // action per match
        Assert.Equal(1, host.SoundCount);           // alert once per line
    }

    [Fact]
    public void MatchAll_skips_zero_width_matches()
    {
        var (host, _, trig) = Make();
        // \d* matches the empty string at many positions; only the real digit run
        // should fire the action (zero-length matches are skipped).
        trig.AddTrigger(@"\d*", "x $0", matchAll: true);
        trig.ProcessLine("a5b");

        Assert.Equal(new[] { "x 5" }, host.GameCommands);
    }

    [Fact]
    public void MatchAll_combines_with_eval()
    {
        var (host, _, trig) = Make();
        trig.AddTrigger(@"(\d+) gold", "deposit {$1 - 1}", eval: true, matchAll: true);
        trig.ProcessLine("5 gold and 10 gold");

        Assert.Equal(new[] { "deposit 4", "deposit 9" }, host.GameCommands);
    }

    [Fact]
    public void Command_add_with_matchall_keyword_sets_the_flag()
    {
        var (_, cmd, trig) = Make();
        cmd.ProcessInput("#trigger add pattern action matchall");

        var rule = trig.Triggers.FirstOrDefault(t => t.Pattern == "pattern");
        Assert.NotNull(rule);
        Assert.True(rule!.MatchAll);
        Assert.False(rule.Eval);
        Assert.Equal("action", rule.Action);
    }

    [Fact]
    public void Command_add_accepts_both_eval_and_matchall()
    {
        var (_, cmd, trig) = Make();
        cmd.ProcessInput("#trigger add pattern action eval matchall");

        var rule = trig.Triggers.FirstOrDefault(t => t.Pattern == "pattern");
        Assert.NotNull(rule);
        Assert.True(rule!.Eval);
        Assert.True(rule.MatchAll);
    }

    [Fact]
    public void Save_then_load_cfg_round_trips_matchall()
    {
        var (_, cmd, trig) = Make();
        cmd.ProcessInput("#trigger add ^a$ look matchall");
        cmd.ProcessInput("#trigger add ^b$ look");
        cmd.ProcessInput("#trigger save");

        trig.Clear();
        cmd.ProcessInput("#trigger load");

        Assert.True(trig.Triggers.First(t => t.Pattern == "^a$").MatchAll);
        Assert.False(trig.Triggers.First(t => t.Pattern == "^b$").MatchAll);
    }

    /// <summary>Records every command dispatched to the game and counts sound plays.</summary>
    private sealed class FakeCommandHost : ICommandHost
    {
        public List<string> GameCommands { get; } = new();
        public int SoundCount { get; private set; }
        public Dictionary<string, string> Globals { get; } = new();

        public IReadOnlyDictionary<string, string> GetGlobalVariables() => Globals;
        public string ExpandVariables(string text) => text;

        public void SendToGame(string text, bool userInput = false, string origin = "", string? echoOverride = null)
            => GameCommands.Add(text);
        public void PlaySound(string soundName) => SoundCount++;

        public void Echo(string text) { }
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
        public IReadOnlyList<string> RunningScripts() => Array.Empty<string>();
        public void SetGlobalVariable(string name, string value) => Globals[name] = value;
        public void RemoveGlobalVariable(string name) => Globals.Remove(name);
        public string SetLiveAudit(Genie.Core.Diagnostics.AuditMode mode) => string.Empty;
        public void EditScript(string name) { }
        public void LayoutCommand(string args) { }
        public void PluginCommand(string args) { }
        public void ConfigCommand(string args) { }
        public void MapperGoto(string args) { }
        public void MapperCommand(string args) { }
        public void MapperReset() { }
        public void Speak(string text, bool urgent = false) { }
        public void TtsCommand(string args) { }
        public void FlashWindow() { }
        public void Connect(ConnectRequest request) { }
    }
}

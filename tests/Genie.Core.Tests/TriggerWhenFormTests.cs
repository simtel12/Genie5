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
/// #162 — Genie 4's action-first typed form <c>#action {command} when {pattern}</c>.
/// The typed path used to read arguments positionally as pattern-first, so the
/// when-form stored the rule transposed: pattern=<c>stand</c>, action=<c>when</c>,
/// class=<c>You stumble…</c> — a trigger that fired the literal command "when"
/// whenever "stand" appeared in game text. These tests pin both argument orders.
/// </summary>
public class TriggerWhenFormTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;

    public TriggerWhenFormTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_trigwhen_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieTrigWhenTest", _root);
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
    public void Action_when_form_stores_action_and_pattern_the_right_way_round()
    {
        var (_, cmd, trig) = Make();
        cmd.ProcessInput("#action {stand} when {You stumble to the ground}");

        var rule = trig.Triggers.FirstOrDefault(t => t.Pattern == "You stumble to the ground");
        Assert.NotNull(rule);
        Assert.Equal("stand", rule!.Action);
        Assert.Equal("", rule.ClassName);
        // No transposed rule was created alongside it.
        Assert.DoesNotContain(trig.Triggers, t => t.Pattern == "stand" || t.Action == "when");
    }

    [Fact]
    public void Action_when_form_fires_the_command_when_the_pattern_matches()
    {
        var (host, cmd, trig) = Make();
        cmd.ProcessInput("#action {stand} when {You stumble to the ground}");
        trig.ProcessLine("You stumble to the ground in a heap.");

        Assert.Equal("stand", host.LastGameCommand);
    }

    [Fact]
    public void Trigger_positional_form_is_unchanged()
    {
        var (_, cmd, trig) = Make();
        cmd.ProcessInput("#trigger {You stumble to the ground} {stand} {combat}");

        var rule = trig.Triggers.FirstOrDefault(t => t.Pattern == "You stumble to the ground");
        Assert.NotNull(rule);
        Assert.Equal("stand", rule!.Action);
        Assert.Equal("combat", rule.ClassName);
    }

    [Fact]
    public void Whenre_keyword_is_accepted_as_a_synonym()
    {
        var (_, cmd, trig) = Make();
        cmd.ProcessInput(@"#action {stand} whenre {^You (stumble|fall)}");

        var rule = trig.Triggers.FirstOrDefault(t => t.Action == "stand");
        Assert.NotNull(rule);
        Assert.Equal("^You (stumble|fall)", rule!.Pattern);
    }

    [Fact]
    public void Unbraced_multiword_command_before_when_is_rejoined()
    {
        var (host, cmd, trig) = Make();
        cmd.ProcessInput("#action put stand when {You stumble}");
        trig.ProcessLine("You stumble badly.");

        Assert.Equal("put stand", host.LastGameCommand);
    }

    /// <summary>Records the last command dispatched to the game.</summary>
    private sealed class FakeCommandHost : ICommandHost
    {
        public string? LastGameCommand { get; private set; }
        public Dictionary<string, string> Globals { get; } = new();

        public IReadOnlyDictionary<string, string> GetGlobalVariables() => Globals;
        public string ExpandVariables(string text) => text;

        public void SendToGame(string text, bool userInput = false, string origin = "", string? echoOverride = null)
            => LastGameCommand = text;

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
        public void PlaySound(string soundName) { }
        public void Speak(string text, bool urgent = false) { }
        public void TtsCommand(string args) { }
        public void FlashWindow() { }
        public void Connect(ConnectRequest request) { }
    }
}

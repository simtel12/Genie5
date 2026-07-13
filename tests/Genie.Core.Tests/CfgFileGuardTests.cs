using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genie.Core.Commanding;
using Genie.Core.Config;
using Genie.Core.Queue;
using Genie.Core.Runtime;
using Genie.Core.Substitutes;
using Genie.Core.Triggers;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Guards around loading saved .cfg rule files. The loaders used to replay
/// every file line through ProcessInput, whose fallthrough is send-to-game —
/// when the Genie 4 Import dialog wrote JSON into .cfg-named files, a connect
/// dispatched hundreds of JSON fragments to the game server as commands.
/// These tests pin: (1) JSON-format saves are detected, deserialized, applied,
/// and the file is rewritten in cfg format (self-heal); (2) non-#command lines
/// in a cfg file are never sent to the game; (3) well-formed cfg files load
/// exactly as before.
/// </summary>
public class CfgFileGuardTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;

    public CfgFileGuardTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_cfgguard_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieCfgGuardTest", _root);
        lds.UseExplicitRoot(_root);
        _config = new GenieConfig(lds);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    private (FakeCommandHost host, CommandEngine cmd, TriggerEngineFinal trig, SubstituteEngine subs) Make()
    {
        var host = new FakeCommandHost();
        var cmd  = new CommandEngine(_config, new CommandQueue(), new EventQueue(), host);
        var trig = new TriggerEngineFinal(host, cmd);
        var subs = new SubstituteEngine();
        cmd.Triggers    = trig;
        cmd.Substitutes = subs;
        return (host, cmd, trig, subs);
    }

    private string CfgPath(string fileName) => Path.Combine(_config.ConfigProfileDir, fileName);

    private void WriteCfg(string fileName, string content)
    {
        Directory.CreateDirectory(_config.ConfigProfileDir);
        File.WriteAllText(CfgPath(fileName), content);
    }

    [Fact]
    public void Json_substitutes_cfg_heals_loads_and_rewrites_as_cfg()
    {
        var (host, cmd, _, subs) = Make();
        WriteCfg("substitutes.cfg", """
            [
              {
                "Pattern": "fratvarit",
                "Replacement": "fratvarit (drink)",
                "CaseSensitive": true,
                "IsEnabled": true,
                "ClassName": "racial"
              }
            ]
            """);

        cmd.ProcessInput("#substitute load");

        // Rule applied to the engine.
        var rule = subs.Rules.FirstOrDefault(r => r.Pattern == "fratvarit");
        Assert.NotNull(rule);
        Assert.Equal("fratvarit (drink)", rule!.Replacement);
        Assert.Equal("racial", rule.ClassName);

        // Nothing was sent to the game — this is the server-spam regression.
        Assert.Empty(host.SentToGame);

        // The file was rewritten in cfg format, so the next load is normal.
        var healed = File.ReadAllText(CfgPath("substitutes.cfg"));
        Assert.StartsWith("#substitute add", healed);
        Assert.DoesNotContain("\"Pattern\"", healed);
    }

    [Fact]
    public void Json_triggers_cfg_heals_with_flags_preserved()
    {
        var (host, cmd, trig, _) = Make();
        WriteCfg("triggers.cfg", """
            [
              { "Pattern": "^You fall", "Action": "stand", "IsEnabled": true, "ClassName": "combat", "Eval": false, "MatchAll": true }
            ]
            """);

        cmd.ProcessInput("#trigger load");

        var rule = trig.Triggers.FirstOrDefault(t => t.Pattern == "^You fall");
        Assert.NotNull(rule);
        Assert.Equal("stand", rule!.Action);
        Assert.Equal("combat", rule.ClassName);
        Assert.True(rule.MatchAll);
        Assert.Empty(host.SentToGame);
        Assert.StartsWith("#trigger add", File.ReadAllText(CfgPath("triggers.cfg")));
    }

    [Fact]
    public void Healed_file_round_trips_on_second_load()
    {
        var (host, cmd, _, subs) = Make();
        WriteCfg("substitutes.cfg",
            "[ { \"Pattern\": \"gultne ava\", \"Replacement\": \"gultne ava (long pole axe)\", \"IsEnabled\": true, \"ClassName\": \"racial\" } ]");

        cmd.ProcessInput("#substitute load");   // heal + rewrite
        cmd.ProcessInput("#substitute load");   // plain cfg load of the healed file

        var rule = subs.Rules.FirstOrDefault(r => r.Pattern == "gultne ava");
        Assert.NotNull(rule);
        Assert.Equal("gultne ava (long pole axe)", rule!.Replacement);
        Assert.Empty(host.SentToGame);
    }

    [Fact]
    public void NonCommand_lines_in_a_cfg_file_are_skipped_not_sent()
    {
        var (host, cmd, trig, _) = Make();
        WriteCfg("triggers.cfg",
            "#trigger add {^You fall} {stand}\n" +
            "this line is garbage and must never reach the server\n" +
            "#trigger add {^You stumble} {stand}\n");

        cmd.ProcessInput("#trigger load");

        Assert.Empty(host.SentToGame);
        Assert.Equal(2, trig.Triggers.Count(t => t.Action == "stand"));
        Assert.Contains(host.Echoes, e => e.Contains("skipped 1 line"));
    }

    [Fact]
    public void Corrupt_json_cfg_is_reported_and_not_dispatched()
    {
        var (host, cmd, _, subs) = Make();
        WriteCfg("substitutes.cfg", "[ { \"Pattern\": \"broken\", ");   // truncated JSON

        cmd.ProcessInput("#substitute load");

        Assert.Empty(host.SentToGame);
        Assert.Empty(subs.Rules);
        Assert.Contains(host.Echoes, e => e.Contains("no readable entries"));
        // The unreadable original is left in place, not clobbered with an empty rewrite.
        Assert.Contains("broken", File.ReadAllText(CfgPath("substitutes.cfg")));
    }

    /// <summary>Records every game dispatch and echo.</summary>
    private sealed class FakeCommandHost : ICommandHost
    {
        public List<string> SentToGame { get; } = new();
        public List<string> Echoes     { get; } = new();
        public Dictionary<string, string> Globals { get; } = new();

        public IReadOnlyDictionary<string, string> GetGlobalVariables() => Globals;
        public string ExpandVariables(string text) => text;

        public void SendToGame(string text, bool userInput = false, string origin = "", string? echoOverride = null)
            => SentToGame.Add(text);

        public void Echo(string text) => Echoes.Add(text);
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

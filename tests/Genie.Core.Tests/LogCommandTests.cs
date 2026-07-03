using System;
using System.Collections.Generic;
using System.IO;
using Genie.Core.Commanding;
using Genie.Core.Config;
using Genie.Core.Queue;
using Genie.Core.Runtime;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Genie 4 parity for the <c>#log</c> command (Utility/Log.cs). Two forms:
///   <c>#log text</c>          → append to the default per-character log
///                               <LogDir>/<char><game>_<yyyy-MM-dd>.log, with a
///                               "LOG CREATED" banner on first write; no-op when
///                               no character is known.
///   <c>#log &gt;file text</c> → append to <LogDir>/file (or a raw path), no banner.
/// Before this was implemented, <c>#log</c> fell through to "Unknown command: log"
/// — the spam seen when travel.cmd hit its STOW error paths.
/// </summary>
public class LogCommandTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;

    public LogCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_log_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieLogTest", _root);
        lds.UseExplicitRoot(_root);
        _config = new GenieConfig(lds);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    private CommandEngine NewEngine(FakeCommandHost host) =>
        new(_config, new CommandQueue(), new EventQueue(), host);

    [Fact]
    public void Default_form_writes_per_character_file_with_banner()
    {
        var host = new FakeCommandHost { Globals = { ["charactername"] = "Renucci", ["game"] = "DR" } };
        NewEngine(host).ProcessInput("#log MISSING MATCH IN STOW (travel.cmd)");

        var expected = Path.Combine(_config.LogDir,
            $"RenucciDR_{DateTime.Now:yyyy-MM-dd}.log");
        Assert.True(File.Exists(expected), $"expected log file at {expected}");
        var text = File.ReadAllText(expected);
        Assert.Contains("*** LOG CREATED AT", text);
        Assert.Contains("MISSING MATCH IN STOW (travel.cmd)", text);
    }

    [Fact]
    public void Default_form_appends_and_writes_banner_only_once()
    {
        var host = new FakeCommandHost { Globals = { ["charactername"] = "Renucci", ["game"] = "DR" } };
        var engine = NewEngine(host);
        engine.ProcessInput("#log first line");
        engine.ProcessInput("#log second line");

        var expected = Path.Combine(_config.LogDir, $"RenucciDR_{DateTime.Now:yyyy-MM-dd}.log");
        var lines = File.ReadAllLines(expected);
        Assert.Single(Array.FindAll(lines, l => l.StartsWith("*** LOG CREATED AT")));
        Assert.Contains("first line", lines);
        Assert.Contains("second line", lines);
    }

    [Fact]
    public void Named_form_writes_to_that_file_without_banner()
    {
        var host = new FakeCommandHost { Globals = { ["charactername"] = "Renucci", ["game"] = "DR" } };
        NewEngine(host).ProcessInput("#log >travel.log stow failed");

        var expected = Path.Combine(_config.LogDir, "travel.log");
        Assert.True(File.Exists(expected), $"expected log file at {expected}");
        var text = File.ReadAllText(expected);
        Assert.DoesNotContain("*** LOG CREATED AT", text);
        Assert.Contains("stow failed", text);
    }

    [Fact]
    public void Default_form_is_a_noop_when_no_character_is_known()
    {
        var host = new FakeCommandHost();   // empty globals
        NewEngine(host).ProcessInput("#log nothing to see");

        // Nothing should have been written under LogDir.
        Assert.False(Directory.Exists(_config.LogDir) &&
                     Directory.GetFiles(_config.LogDir).Length > 0);
    }

    [Fact]
    public void Named_form_with_no_text_writes_nothing()
    {
        var host = new FakeCommandHost { Globals = { ["charactername"] = "Renucci", ["game"] = "DR" } };
        NewEngine(host).ProcessInput("#log >travel.log");

        var path = Path.Combine(_config.LogDir, "travel.log");
        Assert.False(File.Exists(path));
    }

    /// <summary>
    /// Minimal <see cref="ICommandHost"/> test double: everything is a no-op
    /// except the globals dictionary (feeds the default-form filename) and
    /// <see cref="ExpandVariables"/> (identity — the test passes literal text).
    /// </summary>
    private sealed class FakeCommandHost : ICommandHost
    {
        public Dictionary<string, string> Globals { get; } = new();

        public IReadOnlyDictionary<string, string> GetGlobalVariables() => Globals;
        public string ExpandVariables(string text) => text;

        public void Echo(string text) { }
        public void EchoTo(string text, string? window, string? color) { }
        public void EchoMain(string text, string? color, bool mono) { }
        public void EchoLink(string text, string command, string? window) { }
        public void EchoClear(string? window) { }
        public void WindowCommand(string sub, string window) { }
        public void SetStatusBar(string text, int index) { }
        public void SendToGame(string text, bool userInput = false, string origin = "", string? echoOverride = null) { }
        public void RunScript(string text) { }
        public void InjectParsedLine(string line) { }
        public void StopScript(string? name) { }
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
        public void MapperReset() { }
        public void PlaySound(string soundName) { }
        public void Speak(string text) { }
        public void TtsCommand(string args) { }
        public void Connect(ConnectRequest request) { }
    }
}

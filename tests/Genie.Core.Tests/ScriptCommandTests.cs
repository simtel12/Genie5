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
/// Genie 4 parity for the <c>#script</c> dispatcher (Command.cs:2188):
/// <c>abort</c> / <c>pause</c> / <c>resume</c> act on the named script (or all
/// scripts for "all" / no name); it NEVER starts a script — scripts start with
/// <c>.name</c>. Before this matched Genie 4, "#script abort foo" was handed to
/// RunScript and tried to start a script named "abort" (hit by mm_train's wait
/// mode, which stops its watched script with "#script abort $MM_WAIT_SCRIPT").
/// </summary>
public class ScriptCommandTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;

    public ScriptCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_script_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieScriptTest", _root);
        lds.UseExplicitRoot(_root);
        _config = new GenieConfig(lds);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    private void Run(FakeCommandHost host, string input) =>
        new CommandEngine(_config, new CommandQueue(), new EventQueue(), host).ProcessInput(input);

    [Fact]
    public void Abort_with_name_stops_that_script_and_starts_nothing()
    {
        // mm_train: put #script abort $MM_WAIT_SCRIPT
        var host = new FakeCommandHost();
        Run(host, "#script abort hunt");

        Assert.Equal("hunt", host.StoppedScript);
        Assert.False(host.StopAllCalled);
        Assert.Null(host.RanScript);
    }

    [Fact]
    public void Abort_all_stops_every_script()
    {
        var host = new FakeCommandHost();
        Run(host, "#script abort all");

        Assert.True(host.StopAllCalled);
        Assert.Null(host.RanScript);
    }

    [Fact]
    public void Pause_and_resume_with_name_target_that_script()
    {
        var host = new FakeCommandHost();
        Run(host, "#script pause hunt");
        Run(host, "#script resume hunt");

        Assert.Equal("hunt", host.PausedScript);
        Assert.Equal("hunt", host.ResumedScript);
    }

    [Fact]
    public void Pause_all_maps_to_the_all_form()
    {
        var host = new FakeCommandHost();
        Run(host, "#script pause all");

        Assert.True(host.PauseCalled);
        Assert.Null(host.PausedScript);
    }

    [Fact]
    public void Unknown_subcommand_lists_scripts_instead_of_running_one()
    {
        var host = new FakeCommandHost();
        Run(host, "#script somescript");

        Assert.Null(host.RanScript);
        Assert.Contains("No scripts running.", host.Echoed);
    }

    [Fact]
    public void Bare_script_lists_scripts()
    {
        var host = new FakeCommandHost();
        Run(host, "#script");

        Assert.Null(host.RanScript);
        Assert.Contains("No scripts running.", host.Echoed);
    }

    /// <summary>ICommandHost double recording script-lifecycle calls.</summary>
    private sealed class FakeCommandHost : ICommandHost
    {
        public string?      RanScript     { get; private set; }
        public string?      StoppedScript { get; private set; }
        public bool         StopAllCalled { get; private set; }
        public bool         PauseCalled   { get; private set; }
        public string?      PausedScript  { get; private set; }
        public string?      ResumedScript { get; private set; }
        public List<string> Echoed        { get; } = new();

        public IReadOnlyDictionary<string, string> GetGlobalVariables() => new Dictionary<string, string>();
        public string ExpandVariables(string text) => text;

        public void Echo(string text) => Echoed.Add(text);
        public void EchoTo(string text, string? window, string? color) { }
        public void EchoMain(string text, string? color, bool mono) { }
        public void EchoLink(string text, string command, string? window) { }
        public void EchoClear(string? window) { }
        public void WindowCommand(string sub, string window) { }
        public void SetStatusBar(string text, int index) { }
        public void SendToGame(string text, bool userInput = false, string origin = "", string? echoOverride = null) { }
        public void RunScript(string text) => RanScript = text;
        public void InjectParsedLine(string line) { }
        public void StopScript(string? name) => StoppedScript = name;
        public void StopAllScripts() => StopAllCalled = true;
        public void PauseAllScripts() { }
        public void ResumeAllScripts() { }
        public void PauseScript(string? name) { PauseCalled = true; PausedScript = name; }
        public void ResumeScript(string? name) => ResumedScript = name;
        public void SetTraceLevelAll(int level) { }
        public IReadOnlyList<string> RunningScripts() => Array.Empty<string>();
        public void SetGlobalVariable(string name, string value) { }
        public void RemoveGlobalVariable(string name) { }
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

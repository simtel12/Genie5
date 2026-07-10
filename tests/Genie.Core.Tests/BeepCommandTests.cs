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
/// <c>#beep</c> / <c>#bell</c> dispatch (Genie 4 Command.cs <c>case "beep"</c> /
/// <c>"bell"</c> → <c>Interaction.Beep()</c>). Both route to
/// <see cref="ICommandHost.Beep"/> and take no arguments; before this they fell
/// through to "Unknown command: beep" (14+ uses across hunt.cmd et al.). The
/// PlaySounds gate lives in the host, so the engine just forwards the call.
/// </summary>
public class BeepCommandTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;

    public BeepCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_beep_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieBeepTest", _root);
        lds.UseExplicitRoot(_root);
        _config = new GenieConfig(lds);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    private (FakeCommandHost host, CommandEngine engine) NewEngine()
    {
        var host = new FakeCommandHost();
        return (host, new CommandEngine(_config, new CommandQueue(), new EventQueue(), host));
    }

    [Fact]
    public void Beep_invokes_the_host_bell()
    {
        var (host, engine) = NewEngine();
        engine.ProcessInput("#beep");
        Assert.Equal(1, host.BeepCalls);
    }

    [Fact]
    public void Bell_is_an_alias_for_beep()
    {
        var (host, engine) = NewEngine();
        engine.ProcessInput("#bell");
        Assert.Equal(1, host.BeepCalls);
    }

    [Fact]
    public void Trailing_arguments_are_ignored_not_echoed_as_unknown()
    {
        // Genie 4 #beep takes no args; extra tokens are harmless. Confirm it
        // still beeps once and never routes to the unknown-command echo.
        var (host, engine) = NewEngine();
        engine.ProcessInput("#beep now");
        Assert.Equal(1, host.BeepCalls);
        Assert.DoesNotContain(host.Echoes, e => e.Contains("Unknown command", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Records <see cref="Beep"/> invocations (overriding the interface
    /// default no-op) and any echoed text; all else is a no-op.</summary>
    private sealed class FakeCommandHost : ICommandHost
    {
        public int BeepCalls { get; private set; }
        public List<string> Echoes { get; } = new();

        public Dictionary<string, string> Globals { get; } = new();
        public IReadOnlyDictionary<string, string> GetGlobalVariables() => Globals;
        public string ExpandVariables(string text) => text;

        public void Beep() => BeepCalls++;
        public void Echo(string text) => Echoes.Add(text);
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

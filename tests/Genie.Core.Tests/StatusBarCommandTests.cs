using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genie.Core.Commanding;
using Genie.Core.Config;
using Genie.Core.Queue;
using Genie.Core.Runtime;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// <c>#statusbar</c> / <c>#status</c> dispatch (Genie 4 Command.cs
/// <c>case "statusbar"</c> parity, #111): <c>[N] {text}</c> writes slot N
/// (default 1), a bare <c>N</c> clears slot N, and the Genie 5 extension
/// <c>clearall</c> — sole argument only — empties all ten slots.
/// </summary>
public class StatusBarCommandTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;

    public StatusBarCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_statusbar_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieStatusBarTest", _root);
        lds.UseExplicitRoot(_root);
        _config = new GenieConfig(lds);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    private static void Run(FakeCommandHost host, GenieConfig config, string input) =>
        new CommandEngine(config, new CommandQueue(), new EventQueue(), host).ProcessInput(input);

    [Fact]
    public void Text_without_slot_number_goes_to_slot_1()
    {
        var host = new FakeCommandHost();
        Run(host, _config, "#statusbar Hunting: 4/10 kills");

        var call = Assert.Single(host.StatusBarCalls);
        Assert.Equal((1, "Hunting: 4/10 kills"), (call.Index, call.Text));
    }

    [Fact]
    public void Leading_slot_number_selects_that_slot()
    {
        var host = new FakeCommandHost();
        Run(host, _config, "#statusbar 5 loot: 12 gems");

        var call = Assert.Single(host.StatusBarCalls);
        Assert.Equal((5, "loot: 12 gems"), (call.Index, call.Text));
    }

    [Fact]
    public void Bare_slot_number_clears_that_slot()
    {
        var host = new FakeCommandHost();
        Run(host, _config, "#statusbar 5");

        var call = Assert.Single(host.StatusBarCalls);
        Assert.Equal((5, ""), (call.Index, call.Text));
    }

    [Fact]
    public void Out_of_range_number_is_treated_as_slot_1_text()
    {
        // Genie 4: only 1-10 are slot selectors; anything else is display text.
        var host = new FakeCommandHost();
        Run(host, _config, "#statusbar 11 counter");

        var call = Assert.Single(host.StatusBarCalls);
        Assert.Equal((1, "11 counter"), (call.Index, call.Text));
    }

    [Fact]
    public void Clearall_empties_all_ten_slots()
    {
        var host = new FakeCommandHost();
        Run(host, _config, "#statusbar clearall");

        Assert.Equal(10, host.StatusBarCalls.Count);
        Assert.Equal(Enumerable.Range(1, 10), host.StatusBarCalls.Select(c => c.Index));
        Assert.All(host.StatusBarCalls, c => Assert.Equal("", c.Text));
    }

    [Fact]
    public void Clearall_is_case_insensitive()
    {
        var host = new FakeCommandHost();
        Run(host, _config, "#statusbar ClearAll");

        Assert.Equal(10, host.StatusBarCalls.Count);
    }

    [Fact]
    public void Clearall_with_trailing_text_is_ordinary_slot_1_text()
    {
        // The bulk clear only fires as the sole argument, so a script can still
        // display a literal "clearall done" message.
        var host = new FakeCommandHost();
        Run(host, _config, "#statusbar clearall done");

        var call = Assert.Single(host.StatusBarCalls);
        Assert.Equal((1, "clearall done"), (call.Index, call.Text));
    }

    [Fact]
    public void Status_synonym_dispatches_the_same_way()
    {
        var host = new FakeCommandHost();
        Run(host, _config, "#status 3 running");

        var call = Assert.Single(host.StatusBarCalls);
        Assert.Equal((3, "running"), (call.Index, call.Text));
    }

    [Fact]
    public void No_arguments_is_a_no_op()
    {
        var host = new FakeCommandHost();
        Run(host, _config, "#statusbar");

        Assert.Empty(host.StatusBarCalls);
    }

    /// <summary>
    /// Minimal <see cref="ICommandHost"/> test double: records every
    /// <see cref="SetStatusBar"/> call in order; everything else is a no-op.
    /// </summary>
    private sealed class FakeCommandHost : ICommandHost
    {
        public List<(string Text, int Index)> StatusBarCalls { get; } = new();

        public Dictionary<string, string> Globals { get; } = new();

        public IReadOnlyDictionary<string, string> GetGlobalVariables() => Globals;
        public string ExpandVariables(string text) => text;

        public void Echo(string text) { }
        public void EchoTo(string text, string? window, string? color) { }
        public void EchoMain(string text, string? color, bool mono) { }
        public void EchoLink(string text, string command, string? window) { }
        public void EchoClear(string? window) { }
        public void WindowCommand(string sub, string window) { }
        public void SetStatusBar(string text, int index) => StatusBarCalls.Add((text, index));
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

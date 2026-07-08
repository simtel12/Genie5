using System;
using System.Collections.Generic;
using System.IO;
using Genie.Core.Commanding;
using Genie.Core.Config;
using Genie.Core.Highlights;
using Genie.Core.Queue;
using Genie.Core.Runtime;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Genie 4 parity for the <c>#names</c> / <c>#name</c> command family (#148):
/// typed add / remove / list / clear plus save / load, driving the same
/// <see cref="NameHighlightEngine"/> the Names config tab edits.
/// </summary>
public class NamesCommandTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;
    private readonly NameHighlightEngine _names = new();

    public NamesCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_names_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieNamesTest", _root);
        lds.UseExplicitRoot(_root);
        _config = new GenieConfig(lds);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    private (FakeCommandHost host, CommandEngine engine) Make()
    {
        var host   = new FakeCommandHost();
        var engine = new CommandEngine(_config, new CommandQueue(), new EventQueue(), host) { Names = _names };
        return (host, engine);
    }

    [Fact]
    public void Add_creates_a_rule_with_fg_and_bg()
    {
        var (host, engine) = Make();
        engine.ProcessInput("#names add Fred green black");

        var rule = _names.Get("Fred");
        Assert.NotNull(rule);
        Assert.Equal("green", rule!.ForegroundColor);
        Assert.Equal("black", rule.BackgroundColor);
        Assert.Contains(host.Echoes, e => e.Contains("Name added: Fred"));
    }

    [Fact]
    public void Add_is_an_upsert_keyed_by_name()
    {
        var (_, engine) = Make();
        engine.ProcessInput("#names add Fred green");
        engine.ProcessInput("#names add Fred red");

        Assert.Single(_names.Rules);
        Assert.Equal("red", _names.Get("Fred")!.ForegroundColor);
    }

    [Fact]
    public void Implicit_positional_form_adds_when_a_colour_is_given()
    {
        var (_, engine) = Make();
        engine.ProcessInput("#names Fred cyan");

        Assert.Equal("cyan", _names.Get("Fred")!.ForegroundColor);
    }

    [Fact]
    public void Singular_name_command_works_too()
    {
        var (_, engine) = Make();
        engine.ProcessInput("#name add Barney yellow");

        Assert.NotNull(_names.Get("Barney"));
    }

    [Fact]
    public void Remove_deletes_the_rule()
    {
        var (host, engine) = Make();
        _names.Add("Fred", "green");
        engine.ProcessInput("#names remove Fred");

        Assert.Null(_names.Get("Fred"));
        Assert.Contains(host.Echoes, e => e.Contains("Name removed: Fred"));
    }

    [Fact]
    public void Unname_deletes_the_rule()
    {
        var (_, engine) = Make();
        _names.Add("Fred", "green");
        engine.ProcessInput("#unname Fred");

        Assert.Null(_names.Get("Fred"));
    }

    [Fact]
    public void Clear_empties_the_list()
    {
        var (_, engine) = Make();
        _names.Add("Fred", "green");
        _names.Add("Barney", "red");
        engine.ProcessInput("#names clear");

        Assert.Empty(_names.Rules);
    }

    [Fact]
    public void List_reports_rules_and_filters()
    {
        var (host, engine) = Make();
        _names.Add("Fred", "green");
        _names.Add("Barney", "red");

        engine.ProcessInput("#names");
        Assert.Contains(host.Echoes, e => e.Contains("Fred"));
        Assert.Contains(host.Echoes, e => e.Contains("Barney"));

        host.Echoes.Clear();
        engine.ProcessInput("#names Fred");   // lone token → filtered list
        Assert.Contains(host.Echoes, e => e.Contains("Fred"));
        Assert.DoesNotContain(host.Echoes, e => e.Contains("Barney"));
    }

    [Fact]
    public void Save_then_load_round_trips_through_names_cfg()
    {
        var (_, engine) = Make();
        _names.Add("Fred", "green", "black");
        engine.ProcessInput("#names save");

        _names.Clear();
        Assert.Empty(_names.Rules);

        engine.ProcessInput("#names load");
        var rule = _names.Get("Fred");
        Assert.NotNull(rule);
        Assert.Equal("green", rule!.ForegroundColor);
        Assert.Equal("black", rule.BackgroundColor);
    }

    /// <summary>Minimal <see cref="ICommandHost"/> double that records echoes.</summary>
    private sealed class FakeCommandHost : ICommandHost
    {
        public List<string> Echoes { get; } = new();
        public Dictionary<string, string> Globals { get; } = new();

        public IReadOnlyDictionary<string, string> GetGlobalVariables() => Globals;
        public string ExpandVariables(string text) => text;

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
        public void MapperReset() { }
        public void PlaySound(string soundName) { }
        public void Speak(string text, bool urgent = false) { }
        public void TtsCommand(string args) { }
        public void FlashWindow() { }
        public void Connect(ConnectRequest request) { }
    }
}

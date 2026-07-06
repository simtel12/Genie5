using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genie.Core.Commanding;
using Genie.Core.Config;
using Genie.Core.Queue;
using Genie.Core.Runtime;
using Genie.Core.Variables;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Genie 4 <c>ParseAllArgs</c> parity (Core/Command.cs:2883): a command VALUE
/// that is itself a result-returning <c>#</c>-command stores the command's
/// RESULT. mm_train routes every menu click through
/// <c>put #var selection {#eval toupper("$selection")}</c> — before this fix
/// the literal text <c>#eval toupper("Magic")</c> was stored, and every later
/// <c>if $selection = …</c> comparison received the junk (the cascade of
/// "bad condition … unexpected '#'" in the Jul 5 live test).
/// </summary>
public class InlineEvalValueTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;

    public InlineEvalValueTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_inleval_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieInlineEvalTest", _root);
        lds.UseExplicitRoot(_root);
        _config = new GenieConfig(lds);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    private (CommandEngine Engine, VariableEngine Vars, FakeCommandHost Host) NewEngine()
    {
        var host   = new FakeCommandHost();
        var engine = new CommandEngine(_config, new CommandQueue(), new EventQueue(), host);
        var vars   = new VariableEngine(engine);
        engine.Variables = vars;
        return (engine, vars, host);
    }

    [Fact]
    public void Var_value_that_is_an_eval_command_stores_the_result()
    {
        // mm_train MenuDisplay: put #var selection {#eval toupper("$selection")}
        // ($selection already expanded upstream — the engine sees the literal).
        var (engine, vars, _) = NewEngine();
        engine.ProcessInput("#var selection {#eval toupper(\"Magic\")}");

        Assert.Equal("MAGIC", vars.Store.Get("selection"));
    }

    [Fact]
    public void Var_value_eval_replacere_spaces_to_underscores()
    {
        // mm_train line 1070: put #var selection {#eval replacere("DIVINATION TOOL", " ", "_")}
        var (engine, vars, _) = NewEngine();
        engine.ProcessInput("#var selection {#eval replacere(\"DIVINATION TOOL\", \" \", \"_\")}");

        Assert.Equal("DIVINATION_TOOL", vars.Store.Get("selection"));
    }

    [Fact]
    public void Var_plain_value_is_stored_verbatim()
    {
        var (engine, vars, _) = NewEngine();
        engine.ProcessInput("#var selection MAIN");

        Assert.Equal("MAIN", vars.Store.Get("selection"));
    }

    [Fact]
    public void Var_value_starting_with_other_hash_command_passes_through()
    {
        // Only #eval / #evalmath produce results; e.g. a stored "#parse …"
        // trigger body must not be executed or mangled.
        var (engine, vars, _) = NewEngine();
        engine.ProcessInput("#var mycmd {#parse hello world}");

        Assert.Equal("#parse hello world", vars.Store.Get("mycmd"));
    }

    [Fact]
    public void Tvar_value_that_is_an_eval_command_stores_the_result()
    {
        // mm_train line 127: put #var MM_DIVINATION_TOOL {#eval tolower("$MM_DIVINATION_TOOL")}
        // (#var at the command bar targets the same store; #tvar covers the
        // session-global path the script engine's put uses.)
        var (engine, _, host) = NewEngine();
        engine.ProcessInput("#tvar MM_DIVINATION_TOOL {#eval tolower(\"VISIONS\")}");

        Assert.Equal("visions", host.Globals["MM_DIVINATION_TOOL"]);
    }

    [Fact]
    public void Evalmath_value_coerces_to_number()
    {
        var (engine, vars, _) = NewEngine();
        engine.ProcessInput("#var total {#evalmath 2 + 3}");

        Assert.Equal("5", vars.Store.Get("total"));
    }

    /// <summary>ICommandHost double: records Echo lines + globals.</summary>
    private sealed class FakeCommandHost : ICommandHost
    {
        public List<string> Echoed { get; } = new();
        public Dictionary<string, string> Globals { get; } = new();

        public IReadOnlyDictionary<string, string> GetGlobalVariables() => Globals;
        public string ExpandVariables(string text) => text;

        public void Echo(string text) => Echoed.Add(text);
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
        public void PauseScript(string? name) { }
        public void ResumeScript(string? name) { }
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

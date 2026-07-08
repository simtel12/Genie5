using System;
using System.Collections.Generic;
using System.IO;
using Genie.Core.Commanding;
using Genie.Core.Config;
using Genie.Core.Queue;
using Genie.Core.Runtime;
using Genie.Core.Triggers;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// #150 — opt-in script-expression evaluation of trigger actions. When a rule's
/// <c>eval</c> flag is set, <c>{…}</c> expression blocks in the action are
/// evaluated (after $capture substitution) before dispatch; default-off rules
/// are dispatched verbatim (existing behaviour preserved).
/// </summary>
public class TriggerEvalTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;

    public TriggerEvalTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_trigeval_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieTrigEvalTest", _root);
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
    public void Eval_trigger_evaluates_a_brace_expression()
    {
        var (host, _, trig) = Make();
        trig.AddTrigger("^bank$", "put deposit {100 + 5}", eval: true);
        trig.ProcessLine("bank");

        Assert.Equal("put deposit 105", host.LastGameCommand);
    }

    [Fact]
    public void Eval_trigger_combines_capture_and_expression()
    {
        var (host, _, trig) = Make();
        trig.AddTrigger(@"(\d+) coins", "deposit {$1 - 10}", eval: true);
        trig.ProcessLine("You have 50 coins.");

        Assert.Equal("deposit 40", host.LastGameCommand);
    }

    [Fact]
    public void Eval_trigger_resolves_a_global_variable_inside_the_block()
    {
        var (host, _, trig) = Make();
        host.Globals["fee"] = "7";
        trig.AddTrigger("^pay$", "give {$fee * 2}", eval: true);
        trig.ProcessLine("pay");

        Assert.Equal("give 14", host.LastGameCommand);
    }

    [Fact]
    public void NonEval_trigger_leaves_braces_literal()
    {
        var (host, _, trig) = Make();
        trig.AddTrigger("^bank$", "put deposit {100 + 5}");   // eval defaults off
        trig.ProcessLine("bank");

        Assert.Equal("put deposit {100 + 5}", host.LastGameCommand);
    }

    [Fact]
    public void Malformed_expression_falls_back_to_inner_text()
    {
        var (host, _, trig) = Make();
        trig.AddTrigger("^say$", "say {not a + valid + }", eval: true);
        trig.ProcessLine("say");

        // Braces dropped, inner text preserved — the action still dispatches.
        Assert.StartsWith("say ", host.LastGameCommand);
        Assert.DoesNotContain("{", host.LastGameCommand);
    }

    [Fact]
    public void Command_add_with_eval_keyword_sets_the_flag()
    {
        var (_, cmd, trig) = Make();
        cmd.ProcessInput("#trigger add pattern action eval");

        var rule = System.Linq.Enumerable.FirstOrDefault(trig.Triggers, t => t.Pattern == "pattern");
        Assert.NotNull(rule);
        Assert.True(rule!.Eval);
        Assert.Equal("action", rule.Action);   // 'eval' consumed, not treated as the action's class
    }

    [Fact]
    public void Command_add_without_eval_keyword_leaves_flag_off()
    {
        var (_, cmd, trig) = Make();
        cmd.ProcessInput("#trigger add pattern action myclass");

        var rule = System.Linq.Enumerable.FirstOrDefault(trig.Triggers, t => t.Pattern == "pattern");
        Assert.NotNull(rule);
        Assert.False(rule!.Eval);
        Assert.Equal("myclass", rule.ClassName);
    }

    [Fact]
    public void Save_then_load_cfg_round_trips_the_eval_flag()
    {
        var (_, cmd, trig) = Make();
        cmd.ProcessInput("#trigger add ^bank$ {put deposit coins} eval");
        cmd.ProcessInput("#trigger add ^plain$ look");
        cmd.ProcessInput("#trigger save");

        trig.Clear();
        Assert.Empty(trig.Triggers);

        cmd.ProcessInput("#trigger load");
        var evalRule  = System.Linq.Enumerable.FirstOrDefault(trig.Triggers, t => t.Pattern == "^bank$");
        var plainRule = System.Linq.Enumerable.FirstOrDefault(trig.Triggers, t => t.Pattern == "^plain$");
        Assert.NotNull(evalRule);
        Assert.True(evalRule!.Eval);
        Assert.NotNull(plainRule);
        Assert.False(plainRule!.Eval);
    }

    /// <summary>Records the last command dispatched to the game and resolves
    /// <c>$name</c> globals for the expression-with-variable test.</summary>
    private sealed class FakeCommandHost : ICommandHost
    {
        public string? LastGameCommand { get; private set; }
        public Dictionary<string, string> Globals { get; } = new();

        public IReadOnlyDictionary<string, string> GetGlobalVariables() => Globals;

        public string ExpandVariables(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('$') < 0) return text;
            var sb = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '$') { sb.Append(text[i]); continue; }
                int j = i + 1;
                while (j < text.Length && (char.IsLetterOrDigit(text[j]) || text[j] == '_')) j++;
                var name = text[(i + 1)..j];
                if (name.Length > 0 && Globals.TryGetValue(name, out var v)) { sb.Append(v); i = j - 1; }
                else sb.Append('$');
            }
            return sb.ToString();
        }

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
        public void MapperReset() { }
        public void PlaySound(string soundName) { }
        public void Speak(string text, bool urgent = false) { }
        public void TtsCommand(string args) { }
        public void FlashWindow() { }
        public void Connect(ConnectRequest request) { }
    }
}

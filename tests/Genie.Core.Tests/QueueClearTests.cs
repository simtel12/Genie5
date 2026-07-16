using System;
using System.Collections.Generic;
using System.IO;
using Genie.Core.Commanding;
using Genie.Core.Config;
using Genie.Core.Queue;
using Genie.Core.Runtime;
using Genie.Core.Scripting;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Genie 4's <c>#queue clear</c>: flush every command that is waiting to be
/// sent — the RT-gated <c>CommandQueue</c> plus each running script's pending
/// put/send segments — without touching anything already on the wire.
/// travel.cmd's RETURN_CLEAR issues it before rerouting; in G5 it used to echo
/// "Unknown command" and clear nothing.
/// </summary>
public class QueueClearTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;

    public QueueClearTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_queue_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieQueueTest", _root);
        lds.UseExplicitRoot(_root);
        _config = new GenieConfig(lds);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    [Fact]
    public void Queue_clear_flushes_command_queue_and_notifies_host()
    {
        var host  = new FakeCommandHost();
        var queue = new CommandQueue();
        var engine = new CommandEngine(_config, queue, new EventQueue(), host);

        queue.AddToQueue(5, "say hi", waitForRoundtime: true, waitForWebbed: false, waitForStunned: false);
        queue.AddToQueue(5, "say ho", waitForRoundtime: true, waitForWebbed: false, waitForStunned: false);
        Assert.Equal(2, queue.EventList.Count);

        engine.ProcessInput("#queue clear");

        Assert.Empty(queue.EventList);
        Assert.Equal(1, host.ClearSendQueueCalls);
        Assert.Empty(host.GameCommands);   // never reaches the game
    }

    [Fact]
    public void Queue_without_clear_echoes_usage_only()
    {
        var host  = new FakeCommandHost();
        var queue = new CommandQueue();
        var engine = new CommandEngine(_config, queue, new EventQueue(), host);

        engine.ProcessInput("#queue");
        engine.ProcessInput("#queue wibble");

        Assert.Equal(0, host.ClearSendQueueCalls);
        Assert.Empty(host.GameCommands);
    }

    [Fact]
    public void ClearPendingSends_drops_queued_script_segments()
    {
        var sent = new List<string>();
        var dir = Path.Combine(Path.GetTempPath(), "gc_qclear_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Three game-bound segments: the first goes out inline; the rest
            // wait in PendingSends for a game prompt that never comes here.
            File.WriteAllText(Path.Combine(dir, "t.cmd"),
                "put one;two;three\n" +
                "pause 5\n");
            var engine = new ScriptEngine(dir, new TypeAheadSession(),
                                          sendCommand: sent.Add, echo: _ => { });
            engine.TryStart("t", new List<string>());
            for (int i = 0; i < 50; i++) engine.Tick();
            Assert.Equal(new[] { "one" }, sent);

            engine.ClearPendingSends();
            engine.OnPrompt();              // budget frees — nothing left to drain
            for (int i = 0; i < 50; i++) engine.Tick();

            Assert.Equal(new[] { "one" }, sent);
        }
        finally { try { Directory.Delete(dir, true); } catch { /* best effort */ } }
    }

    private sealed class FakeCommandHost : ICommandHost
    {
        public int ClearSendQueueCalls { get; private set; }
        public List<string> GameCommands { get; } = new();
        public Dictionary<string, string> Globals { get; } = new();

        public void ClearSendQueue() => ClearSendQueueCalls++;

        public IReadOnlyDictionary<string, string> GetGlobalVariables() => Globals;
        public string ExpandVariables(string text) => text;
        public void SendToGame(string text, bool userInput = false, string origin = "", string? echoOverride = null)
            => GameCommands.Add(text);

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
        public void MapperCommand(string args) { }
        public void PlaySound(string soundName) { }
        public void Speak(string text, bool urgent = false) { }
        public void TtsCommand(string args) { }
        public void FlashWindow() { }
        public void Connect(ConnectRequest request) { }
    }
}

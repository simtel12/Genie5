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
/// Public #179 — <c>#comment &lt;window&gt; &lt;text&gt;</c> (Genie 4 window
/// title annotation). A common trigger use is <c>#comment Room $zoneid. $roomid</c>
/// → the Room panel titled "Room (69. 120)". Verifies the command parses the
/// window + comment and routes to the host; a bare window clears it.
/// </summary>
public class CommentCommandTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;

    public CommentCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_comment_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieCommentTest", _root);
        lds.UseExplicitRoot(_root);
        _config = new GenieConfig(lds);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }

    private static void Run(FakeHost host, GenieConfig config, string input) =>
        new CommandEngine(config, new CommandQueue(), new EventQueue(), host).ProcessInput(input);

    [Fact]
    public void Window_and_text_route_to_the_host()
    {
        var host = new FakeHost();
        Run(host, _config, "#comment Room 69. 120");

        var call = Assert.Single(host.CommentCalls);
        Assert.Equal(("Room", "69. 120"), call);
    }

    [Fact]
    public void Multiword_comment_is_joined()
    {
        var host = new FakeHost();
        Run(host, _config, "#comment Mapper heading north now");

        Assert.Equal(("Mapper", "heading north now"), Assert.Single(host.CommentCalls));
    }

    [Fact]
    public void Bare_window_clears_the_comment()
    {
        var host = new FakeHost();
        Run(host, _config, "#comment Room");

        Assert.Equal(("Room", ""), Assert.Single(host.CommentCalls));
    }

    private sealed class FakeHost : ICommandHost
    {
        public List<(string Window, string Comment)> CommentCalls { get; } = new();
        public void SetWindowComment(string window, string comment) => CommentCalls.Add((window, comment));

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

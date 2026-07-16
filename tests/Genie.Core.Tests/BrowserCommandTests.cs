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
/// <c>#browser &lt;url&gt;</c> dispatch (Genie 4 Command.cs <c>case "browser"</c> →
/// <c>LaunchBrowser</c>). Before this the command fell through to "Unknown
/// command: browser", which broke the Inventory View wiki lookup (its Genie 4
/// plugin heritage relies on <c>#browser</c>). Only the argumentless usage path
/// is exercised here — a real URL would launch the OS browser from the test
/// runner.
/// </summary>
public class BrowserCommandTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;

    public BrowserCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_browser_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieBrowserTest", _root);
        lds.UseExplicitRoot(_root);
        _config = new GenieConfig(lds);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    [Fact]
    public void Browser_without_a_url_prints_usage_not_unknown_command()
    {
        var host = new FakeCommandHost();
        var engine = new CommandEngine(_config, new CommandQueue(), new EventQueue(), host);

        engine.ProcessInput("#browser");

        Assert.Contains(host.Echoes, e => e.Contains("Usage: #browser"));
        Assert.DoesNotContain(host.Echoes, e => e.Contains("Unknown command", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Echo-recording host; all else is a no-op.</summary>
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
        public void MapperCommand(string args) { }
        public void MapperReset() { }
        public void PlaySound(string soundName) { }
        public void Speak(string text, bool urgent = false) { }
        public void TtsCommand(string args) { }
        public void FlashWindow() { }
        public void Connect(ConnectRequest request) { }
    }
}

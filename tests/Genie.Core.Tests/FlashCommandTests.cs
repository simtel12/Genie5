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
/// Genie 4 parity for <c>#flash</c> (Command.cs: <c>case "flash":
/// EventFlashWindow?.Invoke()</c>): the bare verb asks the host to flash the
/// taskbar / dock entry. Its classic use is a trigger action on whispers, so
/// the command must fire from the shared command pipeline, not just typed
/// input.
/// </summary>
public class FlashCommandTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;

    public FlashCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_flash_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieFlashTest", _root);
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
    public void Flash_invokes_the_host()
    {
        var host = new FakeCommandHost();
        Run(host, _config, "#flash");

        Assert.Equal(1, host.FlashCalls);
    }

    [Fact]
    public void Trailing_arguments_are_ignored_like_genie4()
    {
        // Genie 4's dispatcher matched the verb and ignored the rest.
        var host = new FakeCommandHost();
        Run(host, _config, "#flash now please");

        Assert.Equal(1, host.FlashCalls);
    }

    [Fact]
    public void Unprefixed_flash_is_not_the_command()
    {
        // A bare "flash" is game input (e.g. the FLASH spell prep), never #flash.
        var host = new FakeCommandHost();
        Run(host, _config, "flash");

        Assert.Equal(0, host.FlashCalls);
    }

    /// <summary>ICommandHost double: counts FlashWindow calls; all else no-op.</summary>
    private sealed class FakeCommandHost : ICommandHost
    {
        public int FlashCalls { get; private set; }

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
        public void FlashWindow() { FlashCalls++; }
        public void Connect(ConnectRequest request) { }
    }
}

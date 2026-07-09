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
/// Genie 4 parity for <c>#clear [window]</c> (Command.cs:
/// <c>ClearWindow(ParseAllArgs(oArgs, 1))</c>): the window name is the whole
/// remainder and needs NO ">" prefix. Before this was fixed, any target that
/// didn't start with ">" fell through to "clear the main game window" — so
/// mm_train's menu redraw (<c>#clear "Moonmage Training Menu"</c>, fired from
/// a #link click) blanked the player's Game window.
/// </summary>
public class ClearCommandTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;

    public ClearCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_clear_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieClearTest", _root);
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
    public void Bare_clear_targets_the_main_window()
    {
        var host = new FakeCommandHost();
        Run(host, _config, "#clear");

        Assert.True(host.EchoClearCalled);
        Assert.Null(host.ClearedWindow);
    }

    [Fact]
    public void Redirect_prefix_form_targets_that_window()
    {
        var host = new FakeCommandHost();
        Run(host, _config, "#clear >Menu");

        Assert.Equal("Menu", host.ClearedWindow);
    }

    [Fact]
    public void Quoted_name_without_prefix_targets_that_window_not_main()
    {
        // mm_train's menu redraw — the regression that blanked the Game window.
        var host = new FakeCommandHost();
        Run(host, _config, "#clear \"Moonmage Training Menu\"");

        Assert.Equal("Moonmage Training Menu", host.ClearedWindow);
    }

    [Fact]
    public void Unquoted_multiword_name_is_joined_like_genie4_ParseAllArgs()
    {
        var host = new FakeCommandHost();
        Run(host, _config, "#clear Moonmage Training Menu");

        Assert.Equal("Moonmage Training Menu", host.ClearedWindow);
    }

    [Fact]
    public void Quoted_name_with_redirect_prefix_strips_the_prefix()
    {
        // #link-style ">window" argument reused with #clear.
        var host = new FakeCommandHost();
        Run(host, _config, "#clear \">Moonmage Training Menu\"");

        Assert.Equal("Moonmage Training Menu", host.ClearedWindow);
    }

    [Fact]
    public void Lone_redirect_char_falls_back_to_main()
    {
        var host = new FakeCommandHost();
        Run(host, _config, "#clear >");

        Assert.True(host.EchoClearCalled);
        Assert.Null(host.ClearedWindow);
    }

    /// <summary>
    /// Minimal <see cref="ICommandHost"/> test double: records the
    /// <see cref="EchoClear"/> call; everything else is a no-op.
    /// </summary>
    private sealed class FakeCommandHost : ICommandHost
    {
        public bool    EchoClearCalled { get; private set; }
        public string? ClearedWindow   { get; private set; }

        public Dictionary<string, string> Globals { get; } = new();

        public IReadOnlyDictionary<string, string> GetGlobalVariables() => Globals;
        public string ExpandVariables(string text) => text;

        public void Echo(string text) { }
        public void EchoTo(string text, string? window, string? color) { }
        public void EchoMain(string text, string? color, bool mono) { }
        public void EchoLink(string text, string command, string? window) { }
        public void EchoClear(string? window) { EchoClearCalled = true; ClearedWindow = window; }
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

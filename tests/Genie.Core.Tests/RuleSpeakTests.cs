using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genie.Core.Commanding;
using Genie.Core.Diagnostics;
using Genie.Core.Highlights;
using Genie.Core.Persistence;
using Genie.Core.Triggers;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Public issue #89 — per-rule speak flag on triggers and highlights. A rule's
/// <c>Speak</c> field is: "" = silent, "*" = speak the matched line, anything
/// else = speak that text ($0..$n capture groups expand for triggers). Spoken
/// urgent so hand-picked alerts barge in over stream read-aloud. Must survive
/// both persistence formats (positional .cfg args and the .json models the
/// Configuration dialog uses).
/// </summary>
public class RuleSpeakTests
{
    // ── Trigger fire path ────────────────────────────────────────────────────

    [Fact]
    public void Trigger_SpeakStar_SpeaksMatchedLineUrgent()
    {
        var host = new SpeakCapturingHost();
        var engine = new TriggerEngineFinal(host);
        engine.AddTrigger("has arrived", "", speak: "*");

        engine.ProcessLine("Renucci has arrived.");

        var (text, urgent) = Assert.Single(host.Spoken);
        Assert.Equal("Renucci has arrived.", text);
        Assert.True(urgent);
    }

    [Fact]
    public void Trigger_SpeakText_ExpandsCaptureGroups()
    {
        var host = new SpeakCapturingHost();
        var engine = new TriggerEngineFinal(host);
        engine.AddTrigger(@"^(\w+) whispers,", "", speak: "$1 whispered you");

        engine.ProcessLine("Renucci whispers, \"hey\"");

        var (text, _) = Assert.Single(host.Spoken);
        Assert.Equal("Renucci whispered you", text);
    }

    [Fact]
    public void Trigger_NoSpeak_StaysSilent()
    {
        var host = new SpeakCapturingHost();
        var engine = new TriggerEngineFinal(host);
        engine.AddTrigger("has arrived", "");

        engine.ProcessLine("Renucci has arrived.");

        Assert.Empty(host.Spoken);
    }

    [Fact]
    public void Trigger_Disabled_DoesNotSpeak()
    {
        var host = new SpeakCapturingHost();
        var engine = new TriggerEngineFinal(host);
        engine.AddTrigger("has arrived", "", speak: "*");
        engine.SetEnabled("has arrived", false);

        engine.ProcessLine("Renucci has arrived.");

        Assert.Empty(host.Spoken);
    }

    // ── Defaults ─────────────────────────────────────────────────────────────

    [Fact]
    public void Rules_DefaultSpeak_IsEmpty()
    {
        Assert.Equal("", new TriggerRule("p", "a").Speak);
        Assert.Equal("", new HighlightRule("p", "Red").Speak);
    }

    // ── JSON persistence round-trip (Configuration dialog / connect load) ────

    [Fact]
    public void Triggers_JsonRoundTrip_KeepsSoundAndSpeak()
    {
        var engine = new TriggerEngineFinal();
        engine.AddTrigger("pat", "act", className: "combat", soundFile: "alert.wav", speak: "heads up");

        var p = new PersistenceService();
        var path = Path.Combine(Path.GetTempPath(), $"genie5-test-triggers-{Guid.NewGuid():N}.json");
        try
        {
            p.SaveTriggers(path, engine.Triggers);
            var loaded = Assert.Single(p.LoadTriggers(path));
            Assert.Equal("alert.wav", loaded.SoundFile);
            Assert.Equal("heads up", loaded.Speak);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Highlights_JsonRoundTrip_KeepsSoundAndSpeak()
    {
        var engine = new HighlightEngine();
        engine.AddRule("pat", "Red", soundFile: "ding.wav", speak: "*");

        var p = new PersistenceService();
        var path = Path.Combine(Path.GetTempPath(), $"genie5-test-highlights-{Guid.NewGuid():N}.json");
        try
        {
            p.SaveHighlights(path, engine.Rules);
            var loaded = Assert.Single(p.LoadHighlights(path));
            Assert.Equal("ding.wav", loaded.SoundFile);
            Assert.Equal("*", loaded.Speak);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Triggers_JsonWithoutSpeakFields_LoadsAsEmpty()
    {
        // Pre-#89 files have no SoundFile/Speak properties — they must load
        // clean (missing members default), not throw or go null.
        var path = Path.Combine(Path.GetTempPath(), $"genie5-test-legacy-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """[{"Pattern":"p","Action":"a","IsEnabled":true}]""");
            var loaded = Assert.Single(new PersistenceService().LoadTriggers(path));
            Assert.Equal("", loaded.SoundFile);
            Assert.Equal("", loaded.Speak);
        }
        finally { File.Delete(path); }
    }

    /// <summary>ICommandHost double that records Speak calls; all else no-op.</summary>
    private sealed class SpeakCapturingHost : ICommandHost
    {
        public List<(string Text, bool Urgent)> Spoken { get; } = new();
        public void Speak(string text, bool urgent = false) => Spoken.Add((text, urgent));

        public IReadOnlyDictionary<string, string> GetGlobalVariables() => new Dictionary<string, string>();
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
        public void SetGlobalVariable(string name, string value) { }
        public void RemoveGlobalVariable(string name) { }
        public string SetLiveAudit(AuditMode mode) => string.Empty;
        public void EditScript(string name) { }
        public void LayoutCommand(string args) { }
        public void PluginCommand(string args) { }
        public void ConfigCommand(string args) { }
        public void MapperGoto(string args) { }
        public void MapperReset() { }
        public void PlaySound(string soundName) { }
        public void TtsCommand(string args) { }
        public void FlashWindow() { }
        public void Connect(ConnectRequest request) { }
    }
}

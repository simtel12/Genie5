using System.Text.RegularExpressions;
using Genie.Core.Classes;
using Genie.Core.Commanding;

namespace Genie.Core.Triggers;

public sealed class TriggerEngineFinal
{
    private readonly List<TriggerRule> _triggers = new();
    private readonly ICommandHost?     _host;
    private readonly CommandEngine?    _commandEngine;

    /// <summary>
    /// Construct with optional host + command engine. Both are only used when
    /// triggers fire (<see cref="ProcessLine"/>), so offline / draft instances
    /// for the Configuration dialog can pass nulls.
    /// </summary>
    public TriggerEngineFinal(ICommandHost? host = null, CommandEngine? commandEngine = null)
    { _host = host; _commandEngine = commandEngine; }

    public IReadOnlyList<TriggerRule> Triggers => _triggers;
    public ClassEngine? Classes { get; set; }

    /// <summary>Master enable (File ▸ Master Toggles / <c>#config triggers</c>).
    /// When off, <see cref="ProcessLine"/> fires nothing — rules stay loaded.</summary>
    public bool Enabled { get; set; } = true;

    private bool _safetyEnabled = true;
    /// <summary>When true, trigger regexes run with a match-timeout + literal
    /// pre-filter — the main guard against a catastrophic user pattern freezing
    /// the read thread. Toggling rebuilds every trigger's regex.</summary>
    public bool SafetyEnabled
    {
        get => _safetyEnabled;
        set { if (_safetyEnabled == value) return; _safetyEnabled = value; foreach (var t in _triggers) t.Rebuild(value); }
    }

    public TriggerRule AddTrigger(string pattern, string action, bool caseSensitive = false,
                                  bool isEnabled = true, string className = "", string soundFile = "",
                                  string speak = "", bool eval = false)
    {
        var trigger = new TriggerRule(pattern, action, caseSensitive, isEnabled, className, _safetyEnabled, soundFile, speak, eval);
        _triggers.Add(trigger);
        if (!string.IsNullOrEmpty(className)) Classes?.Ensure(className);
        return trigger;
    }

    public bool RemoveTrigger(string pattern) => _triggers.RemoveAll(t => t.Pattern == pattern) > 0;
    public void Clear() => _triggers.Clear();

    public bool SetEnabled(string pattern, bool isEnabled)
    {
        var t = _triggers.FirstOrDefault(t => t.Pattern == pattern);
        if (t is null) return false;
        t.IsEnabled = isEnabled;
        return true;
    }

    public void ProcessLine(string line, bool echoTriggerDebug = true)
    {
        if (!Enabled) return;
        foreach (var trigger in _triggers)
        {
            if (!trigger.IsEnabled) continue;
            if (Classes is not null && !Classes.IsActive(trigger.ClassName)) continue;
            if (trigger.SafeMatch(line) is not { } match) continue;
            // Optional per-trigger SFX (host applies the PlaySounds gate).
            if (!string.IsNullOrEmpty(trigger.SoundFile))
                _host?.PlaySound(trigger.SoundFile);
            // Optional per-trigger TTS: "*" speaks the matched line, anything
            // else speaks that text with $0..$n expanded. Urgent — these are the
            // user's hand-picked alerts, so they barge in over stream read-aloud.
            if (!string.IsNullOrEmpty(trigger.Speak))
                _host?.Speak(trigger.Speak == "*" ? line : ExpandAction(trigger.Speak, match),
                             urgent: true);
            var expandedAction = ExpandAction(trigger.Action, match);
            // Opt-in (#150): evaluate {…} expression blocks as script expressions
            // after capture substitution, before dispatch.
            if (trigger.Eval) expandedAction = EvalBraces(expandedAction);
            // Automated (game-text-driven) — not interactive, so a #var/#tvar in
            // the action sets silently instead of echoing "Variable set:".
            _commandEngine?.ProcessInput(expandedAction, interactive: false);
        }
    }

    private static string ExpandAction(string action, Match match)
    {
        var result = action;
        for (var i = match.Groups.Count - 1; i >= 0; i--)
            result = result.Replace("$" + i, match.Groups[i].Value);
        return result;
    }

    /// <summary>
    /// Evaluate <c>{…}</c> expression blocks in a trigger action (#150). Resolves
    /// innermost-first so nested blocks compute inside-out, and expands any
    /// <c>$variables</c> inside a block (via the host) before evaluating — the
    /// same order as <c>#eval</c> (variables first, then the expression). A block
    /// that fails to evaluate is left as its literal inner text (braces dropped)
    /// rather than aborting the whole action. Runs only for eval-flagged rules.
    /// </summary>
    private string EvalBraces(string action)
    {
        // Bounded loop: each pass collapses one innermost {…}; the guard caps
        // pathological input (unbalanced braces stop the loop naturally).
        for (int guard = 0; guard < 100; guard++)
        {
            int close = action.IndexOf('}');
            if (close < 0) break;
            int open = action.LastIndexOf('{', close);
            if (open < 0) break;                     // stray '}' — leave the rest literal
            var inner    = action.Substring(open + 1, close - open - 1);
            var expanded = _host?.ExpandVariables(inner) ?? inner;
            string value;
            try   { value = Scripting.ScriptExpression.EvalString(expanded, new Scripting.ScriptInstance()); }
            catch { value = expanded; }              // malformed expr → literal inner text
            action = action[..open] + value + action[(close + 1)..];
        }
        return action;
    }
}

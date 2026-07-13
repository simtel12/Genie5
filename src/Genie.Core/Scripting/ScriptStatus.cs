namespace Genie.Core.Scripting;

/// <summary>
/// Structured snapshot of one running script — the Script Manager panel's row
/// model, unified across <c>.cmd</c> and <c>.js</c>. Produced by
/// <see cref="ScriptEngine.GetStatuses"/>; consumers poll on a UI timer (the
/// panel's analogue of the performance overlay's <c>JsRunningStats</c> loop)
/// rather than subscribing to per-field events.
/// </summary>
/// <param name="Name">Script name (file base name).</param>
/// <param name="IsJs">True for a <c>.js</c> script on the threaded runtime.</param>
/// <param name="Paused">User-initiated pause (Script Bar / #script pause).</param>
/// <param name="State">Human-readable blocking state — Running, Paused,
/// MatchWait, WaitFor, WaitEval, Waiting, Moving, Sleeping, Pausing. Mirrors
/// the <c>#script</c> listing's state word.</param>
/// <param name="CurrentLine">Source line number of the most recently executed
/// statement (0 for <c>.js</c>, which has no line-level tick).</param>
/// <param name="SourcePath">Full path of the source file ("" when unknown).</param>
/// <param name="ElapsedSeconds">Wall-clock seconds since the script started.</param>
/// <param name="DebugLevel">Per-script debug/trace level (always 0 for .js).</param>
/// <param name="PendingMatchCount">Registered match patterns awaiting a
/// matchwait resolution (.cmd detail-strip data).</param>
/// <param name="GosubDepth">Current gosub call-stack depth (.cmd).</param>
/// <param name="PendingReload">True when a <c>#script reload</c> is armed and
/// waiting for the script's next <c>goto</c>.</param>
public sealed record ScriptStatus(
    string Name,
    bool   IsJs,
    bool   Paused,
    string State,
    int    CurrentLine,
    string SourcePath,
    double ElapsedSeconds,
    int    DebugLevel,
    int    PendingMatchCount,
    int    GosubDepth,
    bool   PendingReload);

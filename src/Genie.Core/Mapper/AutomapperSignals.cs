namespace Genie.Core.Mapper;

/// <summary>
/// The Genie 3/4 "automapper" protocol lines the attended walker injects into
/// the game text stream to tell scripts how a <c>#goto</c> ended. These are a
/// hard backwards-compatibility contract: ~19 community movement scripts
/// (<c>travel.cmd</c>, GenieHunter <c>hunt.cmd</c>, <c>go2</c>, …) arm a
/// <c>matchre</c>/<c>matchwait</c> on them around every <c>put #goto</c> and
/// hang or mis-route if the exact wording changes (public #96).
///
/// <para>The canonical waiter, from <c>travel.cmd</c>:</para>
/// <code>
///   matchre AUTOMOVE_FAILED    ^(?:AUTOMAPPER )?MOVE(?:MENT)? FAILED
///   matchre AUTOMOVE_RETURN    ^YOU HAVE ARRIVED(?:\!)?
///   matchre AUTOMOVE_FAIL_BAIL ^DESTINATION NOT FOUND
///   put #goto %Destination
///   matchwait
/// </code>
///
/// <para>Each constant below is line-start anchored by those regexes, so the
/// emitted line must begin with the signal (no leading prefix). The
/// <see cref="Genie.Core.Scripting.ScriptEngine"/>-facing contract is covered by
/// <c>AutomapperSignalsTests</c>; keep the emit sites in the App's
/// <c>AutoWalkService</c> pointed at these constants, never string literals.</para>
/// </summary>
public static class AutomapperSignals
{
    /// <summary>Success: the walker reached the destination room. Scripts treat
    /// this as "arrived" (<c>^YOU HAVE ARRIVED(?:\!)?</c>).</summary>
    public const string Arrived = "YOU HAVE ARRIVED!";

    /// <summary>Failure: the walk could not proceed (step watchdog timeout,
    /// off-path, or a new <c>#goto</c> superseded it). Matches the community
    /// <c>^(?:AUTOMAPPER )?MOVE(?:MENT)? FAILED</c> retry regex.</summary>
    public const string MovementFailed = "AUTOMAPPER MOVEMENT FAILED";

    /// <summary>Failure: the target room/label could not be resolved or no path
    /// exists. Community scripts bail (not retry) on <c>^DESTINATION NOT
    /// FOUND</c>.</summary>
    public const string DestinationNotFound = "DESTINATION NOT FOUND";
}

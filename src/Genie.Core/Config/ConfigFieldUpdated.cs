namespace Genie.Core.Config;

public enum ConfigFieldUpdated
{
    Reconnect,
    Autolog,
    KeepInput,
    Muted,
    AutoMapper,
    LogDir,
    CheckForUpdates,
    AutoUpdate,
    ClassicConnect,
    ImagesEnabled,
    SizeInputToGame,
    AlwaysOnTop,
    UpdateMapperScripts,
    /// <summary>A built-in tracker toggle changed (spelltimer / showexperience /
    /// showtimetracker) — the host re-syncs each extension's Enabled flag.</summary>
    Trackers,
    /// <summary>A rule-engine master enable changed (highlights / triggers /
    /// substitutes / gags / aliases) — GenieCore re-syncs each engine's
    /// Enabled flag and the File ▸ Master Toggles menu re-reads its checks.</summary>
    MasterToggles
}

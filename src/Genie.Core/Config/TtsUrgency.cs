namespace Genie.Core.Config;

/// <summary>Read-aloud urgency for a game stream — Low yields to everything,
/// High barges in mid-utterance. Lives in Core so per-stream priorities are
/// configurable and testable without a UI reference; numeric values match the
/// App's <c>TtsPriority</c> queue enum so mapping is a cast.</summary>
public enum TtsUrgency
{
    Low = 0,
    Normal = 1,
    High = 2,
}

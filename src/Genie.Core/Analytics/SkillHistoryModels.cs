using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Genie.Core.Analytics;

/// <summary>
/// Record types for the local skill-history store (the Analytics dashboard's
/// data). Three layers, all JSONL (one compact JSON object per line, tolerant
/// readers skip unparseable lines):
///
/// <list type="bullet">
/// <item><see cref="SessionRecord"/> — one line per play session in
///   <c>sessions.jsonl</c> (kept indefinitely; feeds the session list).</item>
/// <item><see cref="SnapshotRecord"/> — throttled intra-session deltas in
///   monthly <c>snapshots-YYYYMM.jsonl</c> shards (feeds XP/hour; folded into
///   daily rollups and deleted past the retention window).</item>
/// <item><see cref="DailyRecord"/> — per-UTC-day rollups in <c>daily.jsonl</c>
///   (kept indefinitely; feeds long-horizon gain curves at ~1 KB/day).</item>
/// </list>
///
/// Every line carries a <c>"t"</c> type tag and <c>"v"</c> schema version so
/// new row types (kills, deaths) can join a file later without migration.
/// All timestamps are UTC; intra-session elapsed comes from a monotonic clock
/// so wall-clock jumps can't produce negative rates. Local-only data: the
/// store holds the user's own character's skill table and nothing else.
/// </summary>
public static class SkillHistoryJson
{
    public const int SchemaVersion = 1;

    /// <summary>Compact single-line JSON, readable regex/UTF-8 escaping —
    /// same encoder culture as <c>PersistenceService</c>.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    /// <summary>Serialize one record as a single JSONL line (no newline).</summary>
    public static string ToLine<T>(T record) => JsonSerializer.Serialize(record, Options);

    /// <summary>Parse one JSONL line, or null when the line is torn/garbage —
    /// per-line tolerance so a crash-truncated tail never poisons the file.</summary>
    public static T? TryParse<T>(string line) where T : class
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        try { return JsonSerializer.Deserialize<T>(line, Options); }
        catch { return null; }
    }

    /// <summary>Ranks-per-hour from a fractional-rank gain over elapsed seconds
    /// (0 when no time has passed — never divides by zero).</summary>
    public static double RanksPerHour(double gain, long seconds) =>
        seconds <= 0 ? 0 : gain / (seconds / 3600.0);
}

/// <summary>Start/end rank+percent for one skill across a session.</summary>
public sealed class SkillSpan
{
    [JsonPropertyName("r0")] public int RankStart { get; set; }
    [JsonPropertyName("p0")] public int PercentStart { get; set; }
    [JsonPropertyName("r1")] public int RankEnd { get; set; }
    [JsonPropertyName("p1")] public int PercentEnd { get; set; }

    /// <summary>Fractional ranks gained across the span — same math as the
    /// Experience window's gain column (<see cref="Extensions.Builtin.ExperienceExtension.GainValue"/>).</summary>
    [JsonIgnore]
    public double Gain =>
        Extensions.Builtin.ExperienceExtension.GainValue(RankEnd, PercentEnd, RankStart, PercentStart);
}

/// <summary>One play session — written at disconnect (or synthesized with
/// <see cref="Recovered"/> set when the app died mid-session).</summary>
public sealed class SessionRecord
{
    [JsonPropertyName("t")] public string Type { get; set; } = "session";
    [JsonPropertyName("v")] public int Version { get; set; } = SkillHistoryJson.SchemaVersion;
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("char")] public string Character { get; set; } = "";
    [JsonPropertyName("acct")] public string Account { get; set; } = "";
    [JsonPropertyName("start")] public DateTime StartUtc { get; set; }
    [JsonPropertyName("end")] public DateTime EndUtc { get; set; }
    /// <summary>Monotonic session length — trusted over end-start.</summary>
    [JsonPropertyName("secs")] public long Seconds { get; set; }
    [JsonPropertyName("replay")] public bool Replay { get; set; }
    /// <summary>True when this summary was reconstructed from snapshots after
    /// a crash (no clean disconnect was recorded).</summary>
    [JsonPropertyName("recovered")] public bool Recovered { get; set; }
    [JsonPropertyName("tdp0")] public int TdpStart { get; set; }
    [JsonPropertyName("tdp1")] public int TdpEnd { get; set; }
    [JsonPropertyName("skills")] public Dictionary<string, SkillSpan> Skills { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore] public double TotalGain
    {
        get { double g = 0; foreach (var s in Skills.Values) g += s.Gain; return g; }
    }
}

/// <summary>One throttled delta snapshot — only skills whose (rank, percent,
/// mindstate) changed since the previous written row; the first row of a
/// session is a forced full table (the baseline).</summary>
public sealed class SnapshotRecord
{
    [JsonPropertyName("t")] public string Type { get; set; } = "snap";
    [JsonPropertyName("v")] public int Version { get; set; } = SkillHistoryJson.SchemaVersion;
    /// <summary>Owning session id — joins snapshots to their session row.</summary>
    [JsonPropertyName("sid")] public string SessionId { get; set; } = "";
    [JsonPropertyName("at")] public DateTime AtUtc { get; set; }
    /// <summary>Monotonic seconds since session start (rate math uses this,
    /// never wall-clock deltas).</summary>
    [JsonPropertyName("el")] public long Elapsed { get; set; }
    [JsonPropertyName("replay")] public bool Replay { get; set; }
    /// <summary>TDP total when known (0 = not yet seen; omitted from JSON).</summary>
    [JsonPropertyName("tdp")] public int Tdp { get; set; }
    /// <summary>skill → [rank, percent, mindstate].</summary>
    [JsonPropertyName("skills")] public Dictionary<string, int[]> Skills { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Per-skill day rollup values.</summary>
public sealed class DailySkill
{
    /// <summary>End-of-day rank / percent (last observation that day).</summary>
    [JsonPropertyName("r")] public int Rank { get; set; }
    [JsonPropertyName("p")] public int Percent { get; set; }
    /// <summary>Fractional ranks gained that day (first→last observation).</summary>
    [JsonPropertyName("gain")] public double Gain { get; set; }
    /// <summary>Seconds between the first and last observation of this skill
    /// that day — a tracked-time denominator for rate estimates.</summary>
    [JsonPropertyName("secs")] public long Seconds { get; set; }
}

/// <summary>One UTC day folded down from snapshots — the permanent low-resolution
/// history that survives raw-snapshot retention pruning.</summary>
public sealed class DailyRecord
{
    [JsonPropertyName("t")] public string Type { get; set; } = "day";
    [JsonPropertyName("v")] public int Version { get; set; } = SkillHistoryJson.SchemaVersion;
    /// <summary>UTC day key, "yyyy-MM-dd".</summary>
    [JsonPropertyName("day")] public string Day { get; set; } = "";
    [JsonPropertyName("sessions")] public int Sessions { get; set; }
    [JsonPropertyName("secs")] public long Seconds { get; set; }
    [JsonPropertyName("skills")] public Dictionary<string, DailySkill> Skills { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Genie.App.Theming;

/// <summary>
/// A named UI theme (#20): a base Avalonia variant (Dark/Light — drives all
/// Fluent-styled controls) plus a palette of semantic colour roles keyed by
/// <see cref="ThemeKeys"/>. Serialises to a human-editable JSON file so
/// users can hand-author or share themes (<c>Config/Themes/*.json</c>).
/// </summary>
public sealed class Theme
{
    /// <summary>Display name; also the file name for custom themes.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// "Dark" or "Light" — sets <c>Application.RequestedThemeVariant</c> so
    /// stock Fluent controls (menus, buttons, text boxes, scrollbars) flip
    /// with the palette.
    /// </summary>
    public string BaseVariant { get; set; } = "Dark";

    /// <summary>
    /// Colour per semantic role: <see cref="ThemeKeys"/> key → <c>#RRGGBB</c>
    /// (or <c>#AARRGGBB</c>) hex. Missing keys fall back to the built-in
    /// Dark values at apply time, so hand-written theme files only need the
    /// roles they want to change.
    /// </summary>
    public Dictionary<string, string> Colors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Built-ins ship in code and can't be deleted/overwritten.</summary>
    [JsonIgnore]
    public bool IsBuiltIn { get; init; }

    public string? Get(string key) => Colors.TryGetValue(key, out var hex) ? hex : null;

    /// <summary>Deep copy (for Save Current As… / duplicate).</summary>
    public Theme Clone(string newName) => new()
    {
        Name        = newName,
        BaseVariant = BaseVariant,
        Colors      = new Dictionary<string, string>(Colors, StringComparer.OrdinalIgnoreCase),
    };

    // ── JSON ──────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static Theme? FromJson(string json)
    {
        try { return JsonSerializer.Deserialize<Theme>(json, Json); }
        catch { return null; }
    }
}

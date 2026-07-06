namespace Genie.Core.Extensions.Builtin;

/// <summary>
/// An Elanthian date/time — year since the Victory of Lanival, month (1–10 of
/// 40 days), day, anlas (0–11 per day) and roisaen (0–29 per anlas) — or, in
/// range mode, a *duration* expressed in those units (used by <c>/timediff</c>).
/// Ported from the Genie 4 Time Tracker's DRDateTime (decompile in
/// <c>_refs/TimeTracker_Genie4_decompiled</c>).
/// </summary>
internal readonly struct DRDateTime
{
    public int Year  { get; }
    public int Month { get; }
    public int Day   { get; }
    public int Anlas { get; }
    public int Rois  { get; }
    private readonly bool _range;

    /// <summary>Andu (4-day week) within the month, 1–10.</summary>
    public int Andu => (Day - 1) / 4 + 1;

    /// <summary>Zero-based day of the 400-day year (0–399).</summary>
    public int DaysSince => Day + (Month - 1) * 40 - 1;

    public DRDateTime(int year, int month, int day, int anlas = 0, int rois = 0)
    {
        if (year < 0)                 throw new FormatException("DRDateTime year must be >= 0.");
        if (month < 1 || month > 10)  throw new FormatException("DRDateTime month must be 1-10.");
        if (day < 1 || day > 40)      throw new FormatException("DRDateTime day must be 1-40.");
        if (anlas < 0 || anlas > 11)  throw new FormatException("DRDateTime anlas must be 0-11.");
        if (rois < 0 || rois > 29)    throw new FormatException("DRDateTime roisaen must be 0-29.");
        Year = year; Month = month; Day = day; Anlas = anlas; Rois = rois;
        _range = false;
    }

    private DRDateTime(int year, int month, int day, int anlas, int rois, bool range)
    {
        Year = year; Month = month; Day = day; Anlas = anlas; Rois = rois;
        _range = range;
    }

    /// <summary>A duration in Elanthian units (components need not be valid
    /// calendar positions — 0 months / 0 days are fine).</summary>
    public static DRDateTime Range(int years, int months, int days, int anlaen, int roisaen)
        => new(years, months, days, anlaen, roisaen, range: true);

    public bool IsRange => _range;

    /// <summary>Parse the Genie 4 command format: <c>YYY-MM-DD</c> with an
    /// optional <c> AA:RR</c> tail (e.g. <c>447-06-34 08:17</c>).</summary>
    public static DRDateTime Parse(string date)
    {
        try
        {
            date = date.Trim();
            var year  = int.Parse(date[..3]);
            date = date[4..];
            var month = int.Parse(date[..2]);
            date = date[3..];
            var day   = int.Parse(date[..2]);
            int anlas = 0, rois = 0;
            if (date.Trim().Length > 2)
            {
                date  = date[3..].Trim();
                anlas = int.Parse(date[..2]);
                date  = date[3..];
                rois  = int.Parse(date[..2]);
            }
            return new DRDateTime(year, month, day, anlas, rois);
        }
        catch (Exception e) when (e is not FormatException)
        {
            throw new FormatException("Invalid DRDateTime format.");
        }
    }

    public override string ToString()
    {
        if (!_range)
            return $"{Year:00}-{Month:00}-{Day:00} {Anlas:00}:{Rois:00}";
        var text = Year  != 0 ? $"{Year} years "    : "";
        text +=    Month != 0 ? $"{Month} months "  : "";
        text +=    Day   != 0 ? $"{Day} days "      : "";
        text +=    Anlas != 0 ? $"{Anlas} anlaen "  : "";
        return text + (Rois != 0 ? $"{Rois} roisaen " : "");
    }
}

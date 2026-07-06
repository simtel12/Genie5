using System.Xml.Linq;

namespace Genie.Core.Extensions.Builtin;

/// <summary>
/// Persistent state for the Time Tracker, mirroring the Genie 4
/// <c>Time_Tracker.xml</c> layout: an &lt;Options&gt; display block plus a
/// &lt;Calculations&gt; block of per-moon calibration offsets that
/// <see cref="TimeCalc"/> accumulates from DR's ambient moonrise/moonset
/// messages. Defaults match the shipped Genie 4 file; a user copy at
/// <c>{Config}/Time_Tracker.xml</c> overrides them at load and is rewritten
/// whenever a calibration lands.
/// </summary>
internal sealed class TimeTrackerOptions
{
    public bool ShowElanthiaTime = true;   // show the Elanthian date/time block
    public bool ShowLongNames    = true;   // "Moliko, the Balance" vs "Moliko"
    public bool UseGameTime      = true;   // 30 s live heartbeat vs. repaint only on game activity
    public bool IncludeAnlasName = true;   // show the "N roisaen before the Anlas of X" line
    public bool IncludeTimeOfDay = true;   // show season / part of day
    public bool LogGameEvents    = false;  // host.Log() on calibrations / day rollover

    // Calculations block — cumulative moon-cycle calibration (seconds), fed
    // back into TimeCalc at load so calibration survives restarts. SunOffset
    // is kept for Genie 4 file parity but the sun table is authoritative.
    public long SunOffset;
    public long KatambaOffset;
    public long XibarOffset;
    public long YavashOffset;

    public const string FileName = "Time_Tracker.xml";

    /// <summary>Load options: the built-in defaults, then a user override file in
    /// <paramref name="configDir"/> if one exists. Never throws — a malformed file
    /// just keeps the defaults.</summary>
    public static TimeTrackerOptions Load(string configDir)
    {
        var o = new TimeTrackerOptions();
        if (string.IsNullOrWhiteSpace(configDir)) return o;
        var path = Path.Combine(configDir, FileName);
        if (File.Exists(path))
        {
            try { TryApply(o, File.ReadAllText(path)); } catch { /* keep defaults */ }
        }
        return o;
    }

    /// <summary>Persist the current options + calibration offsets. Never throws —
    /// a failed save just means the next session starts uncalibrated.</summary>
    public void Save(string configDir)
    {
        if (string.IsNullOrWhiteSpace(configDir)) return;
        try
        {
            Directory.CreateDirectory(configDir);
            new XDocument(
                new XElement("TimeTracker",
                    new XElement("Options",
                        new XElement("ShowElanthiaTime", ShowElanthiaTime),
                        new XElement("ShowLongNames",    ShowLongNames),
                        new XElement("UseGameTime",      UseGameTime),
                        new XElement("IncludeAnlasName", IncludeAnlasName),
                        new XElement("IncludeTimeOfDay", IncludeTimeOfDay),
                        new XElement("LogGameEvents",    LogGameEvents)),
                    new XElement("Calculations",
                        new XElement("SunOffset",     SunOffset),
                        new XElement("KatambaOffset", KatambaOffset),
                        new XElement("XibarOffset",   XibarOffset),
                        new XElement("YavashOffset",  YavashOffset))))
                .Save(Path.Combine(configDir, FileName));
        }
        catch { /* best effort */ }
    }

    private static void TryApply(TimeTrackerOptions o, string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return;
        XElement? root;
        try { root = XDocument.Parse(xml).Root; }
        catch { return; }

        if (root?.Element("Options") is { } opts)
        {
            o.ShowElanthiaTime = Bool(opts, "ShowElanthiaTime", o.ShowElanthiaTime);
            o.ShowLongNames    = Bool(opts, "ShowLongNames",    o.ShowLongNames);
            o.UseGameTime      = Bool(opts, "UseGameTime",      o.UseGameTime);
            o.IncludeAnlasName = Bool(opts, "IncludeAnlasName", o.IncludeAnlasName);
            o.IncludeTimeOfDay = Bool(opts, "IncludeTimeOfDay", o.IncludeTimeOfDay);
            o.LogGameEvents    = Bool(opts, "LogGameEvents",    o.LogGameEvents);
        }
        if (root?.Element("Calculations") is { } calc)
        {
            o.SunOffset     = Long(calc, "SunOffset",     o.SunOffset);
            o.KatambaOffset = Long(calc, "KatambaOffset", o.KatambaOffset);
            o.XibarOffset   = Long(calc, "XibarOffset",   o.XibarOffset);
            o.YavashOffset  = Long(calc, "YavashOffset",  o.YavashOffset);
        }
    }

    private static bool Bool(XElement parent, string name, bool fallback) =>
        bool.TryParse(parent.Element(name)?.Value, out var b) ? b : fallback;

    private static long Long(XElement parent, string name, long fallback) =>
        long.TryParse(parent.Element(name)?.Value, out var v) ? v : fallback;
}

namespace Genie.Core.Extensions.Builtin;

/// <summary>
/// Client-side Elanthian clock and heavenly-body calculator — a faithful port
/// of the Genie 4 Time Tracker's TimeCalc (Barnacus, v1.9.0; ILSpy decompile
/// in <c>_refs/TimeTracker_Genie4_decompiled</c> — the original source is
/// lost). Everything derives from the real UTC clock against fixed epoch
/// constants: each moon is a fixed-period cycle visible for roughly its first
/// half; the sun comes from a per-day-of-year rise/set table. Real orbits
/// drift against the fixed cycles, so <see cref="Calibrate"/> re-anchors a
/// moon whenever DR prints its ambient rise/set message, and the cumulative
/// per-moon offsets are persisted (Time_Tracker.xml Calculations block) so
/// the calibration survives restarts — that is how the Genie 4 plugin kept
/// "lossy" fixed cycles honest for years.
///
/// <para>Deviations from the original, both deliberate: (1) the original's
/// game-time branch was dead code — its <c>GameTime</c> setter was a
/// self-assignment bug, so Genie 4 always computed from the wall clock; only
/// the wall-clock path is ported. (2) sun "calibration" in the original was
/// ephemeral (wiped by the next CalculateTimes, since the sun table is
/// authoritative), so sunrise/sunset messages here just trigger a repaint.</para>
///
/// <para>Not thread-safe — callers serialize access (the extension holds one
/// lock across the heartbeat timer and the game-line thread).</para>
/// </summary>
internal sealed class TimeCalc
{
    /// <summary>Seconds from .NET year 1 to the Elanthian epoch. Inherited
    /// from Genie 4 verbatim (its wall-clock reference constant).</summary>
    private const double Epoch = 63297143100.0;

    private const long SecondsPerDay = 21600;   // 12 anlas × 30 roisaen × 60 s

    private sealed class Moon
    {
        public readonly string Name;
        public readonly long CycleEpoch;     // cycle phase anchor (seconds)
        public readonly long VisibleSecs;    // up for this first slice of the cycle
        public readonly long CycleSecs;
        public long Start;                   // calibration shift applied to the phase
        public long OffsetTotal;             // cumulative calibration — persisted
        public bool IsUp;
        public long Countdown;               // seconds until the next rise/set

        public Moon(string name, long cycleEpoch, long visibleSecs, long cycleSecs, long persistedOffset)
        {
            Name = name; CycleEpoch = cycleEpoch; VisibleSecs = visibleSecs; CycleSecs = cycleSecs;
            Start = persistedOffset; OffsetTotal = persistedOffset;
        }
    }

    private readonly Moon _katamba, _xibar, _yavash;
    private readonly (long Rise, long Set)[] _sunData = new (long, long)[401];

    private DRDateTime _date = new(0, 1, 1);
    private long _theSeconds;    // seconds into the current Elanthian day (0–21599)
    private long _totalSeconds;  // seconds since the Elanthian epoch

    public TimeCalc(long katambaOffset, long xibarOffset, long yavashOffset)
    {
        _katamba = new Moon("Katamba", 49623621, 10603, 21088, katambaOffset);
        _xibar   = new Moon("Xibar",   49697781, 10483, 20848, xibarOffset);
        _yavash  = new Moon("Yavash",  49376779, 10621, 21130, yavashOffset);
        // Genie 4's table: day-length sine wave, solstice offset 98 days. Its
        // day+1 lookup could reach index 400 (a zeroed row, one-day-a-year
        // glitch in the original); mirror day 0 there instead.
        for (var i = 0; i < 400; i++)
        {
            var set = (long)Math.Floor(16200.0 - Math.Sin((i + 98) / 400.0 * 6.283185307) * 1800.0);
            _sunData[i] = (SecondsPerDay - set, set);
        }
        _sunData[400] = _sunData[0];
    }

    // ── current state (valid after Recalculate) ────────────────────────────────

    public DRDateTime Date => _date;

    public bool SunIsUp        { get; private set; }
    public long SunCountdown   { get; private set; }

    public bool KatambaIsUp    => _katamba.IsUp;
    public long KatambaCountdown => _katamba.Countdown;
    public long KatambaOffsetTotal => _katamba.OffsetTotal;
    public bool XibarIsUp      => _xibar.IsUp;
    public long XibarCountdown => _xibar.Countdown;
    public long XibarOffsetTotal => _xibar.OffsetTotal;
    public bool YavashIsUp     => _yavash.IsUp;
    public long YavashCountdown => _yavash.Countdown;
    public long YavashOffsetTotal => _yavash.OffsetTotal;

    /// <summary>Recompute the date, sun and all three moons for the given
    /// instant. Must be called before reading any state.</summary>
    public void Recalculate(DateTime utcNow)
    {
        DateFromDateTime(utcNow);
        RecalcSun();
        RecalcMoon(_katamba);
        RecalcMoon(_xibar);
        RecalcMoon(_yavash);
    }

    /// <summary>Re-anchor a moon to "it just rose / just set right now" (DR
    /// printed the ambient message). Returns the seconds of correction applied
    /// (0 when the model was already within 2 s). The correction accumulates
    /// into the persisted per-moon offset.</summary>
    public long Calibrate(string moonName, bool rose, DateTime utcNow)
    {
        var m = moonName switch
        {
            "Katamba" => _katamba,
            "Xibar"   => _xibar,
            "Yavash"  => _yavash,
            _         => null,
        };
        if (m is null) return 0;

        Recalculate(utcNow);
        var t = m.Countdown;
        // When the model's phase disagrees with the event (it thinks the moon
        // is already in the state the message announces), the countdown points
        // at the *following* transition — pull it back one half-cycle.
        if (rose && m.IsUp)   t -= m.VisibleSecs;
        if (!rose && !m.IsUp) t -= m.CycleSecs - m.VisibleSecs;
        // Genie 4 aligned to countdown-zero, which is 2 s shy of the boundary
        // (its countdowns run to boundary−2); anchor on the boundary itself so
        // a repeated message computes delta 0 and calibration is idempotent.
        var delta = -(t + 2);
        if (Math.Abs(delta) <= 2) return 0;   // aligned — don't churn the config
        m.Start       += delta;
        m.OffsetTotal += delta;
        Recalculate(utcNow);
        return delta;
    }

    private void RecalcMoon(Moon m)
    {
        m.Start = Mod(m.Start, m.CycleSecs);
        var pos = Mod(_totalSeconds - m.Start - m.CycleEpoch, m.CycleSecs);
        // The -2 s fudge on both windows is Genie 4's (10601/21086 for 10603/21088).
        if (pos < m.VisibleSecs) { m.IsUp = true;  m.Countdown = Math.Max(0, m.VisibleSecs - 2 - pos); }
        else                     { m.IsUp = false; m.Countdown = Math.Max(0, m.CycleSecs - 2 - pos); }
    }

    private void RecalcSun()
    {
        var day = _date.DaysSince;
        var (rise, set) = _sunData[day];
        if (_theSeconds >= rise && _theSeconds < set)
        {
            SunIsUp = true;
            SunCountdown = set - _theSeconds;
        }
        else
        {
            SunIsUp = false;
            SunCountdown = _theSeconds < rise
                ? rise - _theSeconds
                : SecondsPerDay - _theSeconds + _sunData[day + 1].Rise;
        }
    }

    private static long Mod(long value, long modulus)
    {
        var r = value % modulus;
        return r < 0 ? r + modulus : r;
    }

    // ── calendar math ───────────────────────────────────────────────────────────

    /// <summary>Set the internal clock to this UTC instant and return the
    /// resulting Elanthian date (1 roisaen = 1 real minute; 1 Elanthian day =
    /// 6 real hours; 400-day year of ten 40-day months).</summary>
    public DRDateTime DateFromDateTime(DateTime utc)
    {
        var days = TimeSpan.FromTicks(utc.Ticks).Days;
        var drBase = (double)(((days * 24L + utc.Hour) * 60L + utc.Minute) * 60L + utc.Second) - Epoch;
        _totalSeconds = (long)Math.Floor(drBase);

        var roisaen = drBase / 60.0;
        var anlas   = roisaen / 30.0;
        var totDays = anlas / 12.0;
        var years   = Math.Floor(totDays / 400.0);
        var doy     = Math.Floor(totDays) - years * 400.0;       // 0–399
        var month   = (int)(doy / 40.0);                          // 0–9
        var day     = (int)(doy - month * 40.0 + 1.0);            // 1–40
        var anlasIn = (int)(Math.Floor(anlas) - (years * 400.0 + doy) * 12.0);
        var roisIn  = (int)(Math.Floor(roisaen) - ((years * 400.0 + doy) * 12.0 + anlasIn) * 30.0);
        _date       = new DRDateTime((int)years + 385, month + 1, day, anlasIn, roisIn);
        _theSeconds = (long)(Math.Floor(drBase) - (years * 400.0 + doy) * SecondsPerDay);
        return _date;
    }

    /// <summary>The real-world local time corresponding to an Elanthian date.</summary>
    public static DateTime LocalDateTimeFrom(DRDateTime d)
    {
        var seconds = ((((d.Year - 385) * 400.0 + d.DaysSince) * 12.0 + d.Anlas) * 30.0 + d.Rois) * 60.0 + Epoch;
        return new DateTime(TimeSpan.FromSeconds(seconds).Ticks, DateTimeKind.Utc).ToLocalTime();
    }

    /// <summary>The span between two real instants, expressed as an Elanthian
    /// duration (range-mode <see cref="DRDateTime"/>).</summary>
    public static DRDateTime DurationBetween(DateTime aUtc, DateTime bUtc)
    {
        var drBase = Math.Abs((aUtc - bUtc).TotalSeconds);
        var roisaen = drBase / 60.0;
        var anlas   = roisaen / 30.0;
        var totDays = anlas / 12.0;
        var years   = Math.Truncate(totDays / 400.0);
        var doy     = Math.Truncate(totDays) - years * 400.0;
        var months  = Math.Truncate(doy / 40.0);
        var day     = Math.Truncate(doy) - months * 40.0;
        var anlasIn = Math.Truncate(anlas) - (years * 400.0 + Math.Truncate(doy)) * 12.0;
        var roisIn  = Math.Truncate(roisaen) - ((years * 400.0 + Math.Truncate(doy)) * 12.0 + Math.Truncate(anlasIn)) * 30.0;
        return DRDateTime.Range((int)years, (int)months, (int)day, (int)anlasIn, (int)roisIn);
    }

    // ── names ───────────────────────────────────────────────────────────────────

    private static readonly string[] AnlasNames =
    {
        "Anduwen", "Starwatch", "Asketi's Hunt", "Berengaria's Touch",
        "Hodierna's Blessing", "Peri'el's Watch", "Dergati's Bane", "Firulf's Flame",
        "Tamsine's Toil", "Meraud's Cloak", "Phelim's Vigil", "Revelfae",
    };

    private static readonly string[] AnduNames =
    {
        "", "Kertandu", "Hodandu", "Evandu", "Truffandu", "Havrandu",
        "Elandu", "Chandu", "Glythandu", "Faenandu", "Tamsandu",
    };

    private static readonly string[] MonthNames =
    {
        "", "Akroeg, the Ram", "Ka'len, the Sea Drake", "Lirisa, the Archer",
        "Shorka, the Cobra", "Uthmor, the Giant", "Arhat, the Fire Lion",
        "Moliko, the Balance", "Skullcleaver, the Dwarven Axe",
        "Dolefaren, the Brigantine", "Nissa, the Maiden",
    };

    private static readonly string[] YearNames =
    {
        "", "Year of the Bronze Wyvern", "Year of the Golden Panther",
        "Year of the Amber Phoenix", "Year of the Iron Toad",
        "Year of the Emerald Dolphin", "Year of the Crystal Snow Hare",
        "Year of the Silver Unicorn",
    };

    private static readonly string[] PartsOfDay =
    {
        "", "night", "approaching sunrise", "dawn", "early morning", "mid-morning",
        "late morning", "midday", "early afternoon", "mid-afternoon", "late afternoon",
        "dusk", "sunset", "early evening", "evening", "late evening",
    };

    public string YearName  => YearNames[_date.Year % 7 == 0 ? 7 : _date.Year % 7];
    public string MonthName => MonthNames[_date.Month];
    public string AnlasName => AnlasNames[_date.Anlas];
    public string NextAnlasName => AnlasNames[(_date.Anlas + 1) % 12];

    /// <summary>"Akroeg, the Ram" → "Akroeg".</summary>
    public static string ShortMonth(string monthName)
    {
        var comma = monthName.IndexOf(',');
        return comma > 0 ? monthName[..comma] : monthName;
    }

    public string Season => _date.DaysSince switch
    {
        < 50  => "winter",
        < 150 => "spring",
        < 250 => "summer",
        < 350 => "fall",
        _     => "winter",
    };

    public string TimeOfDay
    {
        get
        {
            var (rise, set) = _sunData[_date.DaysSince];
            var dayLen   = set - rise + 1;
            var nightLen = SecondsPerDay - dayLen;
            var t = _theSeconds;
            var idx = 1;
            if (t >= rise - nightLen / 8)  idx = 2;
            if (t >= rise)                 idx = 3;
            if (t >= rise + nightLen / 8)  idx = 4;
            if (t >= 10800 - dayLen / 4)   idx = 5;
            if (t >= 10800 - dayLen / 8)   idx = 6;
            if (t >= 10800)                idx = 7;
            if (t >= 10800 + dayLen / 8)   idx = 8;
            if (t >= 10800 + dayLen / 4)   idx = 9;
            if (t >= set - dayLen / 8)     idx = 10;
            if (t >= set - dayLen / 9)     idx = 11;
            if (t >= set)                  idx = 12;
            if (t >= set + nightLen / 9)   idx = 13;
            if (t >= SecondsPerDay - nightLen / 4) idx = 14;
            if (t >= SecondsPerDay - nightLen / 8) idx = 15;
            return PartsOfDay[idx];
        }
    }

    /// <summary>The Genie 4 long-form current-time sentence (the LongNames /
    /// <c>/now</c> output).</summary>
    public string DescriptiveText
    {
        get
        {
            var d = _date;
            var text = $"It is the {YearName}, {d.Year} years since the Victory of Lanival the Redeemer. ";
            text += $"It is the {d.Day}{Suffix(d.Day)} day and the {d.Andu}{Suffix(d.Andu)} andu of {AnduNames[d.Andu]} in ";
            text += $"the {d.Month}{Suffix(d.Month)} month of {MonthName}. ";
            text += $"It is now the {d.Rois}{Suffix(d.Rois)} roisaen of the {d.Anlas}{Suffix(d.Anlas)} anlas of {AnlasName}. ";
            return text + $"It is currently {Season} and {TimeOfDay}. ";
        }
    }

    public static string Suffix(long n)
    {
        var mod100 = n % 100;
        if (mod100 is > 10 and < 20) return "th";
        return (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
    }
}

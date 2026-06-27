namespace Genie.Core.Extensions.Builtin.CircleCalc;

/// <summary>
/// The circle-requirement and skill-sort math, ported faithfully from VTCifer's
/// Genie 4 Circle Calculator (the <c>CalculateReq3_0</c> / <c>CalculateCirclebyXML</c>
/// / <c>SortSkills</c> routines). Pure functions over a guild's requirements and a
/// skill→rank map, returning the lines to echo — no host/UI dependency, so the
/// logic is unit-testable.
/// </summary>
internal static class CircleEngine
{
    /// <param name="TargetCircle">explicit target, or 0 to mean "the next circle up".</param>
    /// <param name="BottomSort">CircleCalc.Sort == 1: list highest-circle reqs first.</param>
    /// <param name="Display">CircleCalc.Display: 0 = up to circle 200, 1 = all, 2 = the next binding circle only.</param>
    internal readonly record struct Options(int TargetCircle, bool BottomSort, int Display);

    private sealed record Req(int Circle, int CurrentCircle, int RanksNeeded, string Name, int Ranks);

    // ── circle calculation ────────────────────────────────────────────────────────

    public static List<string> Calculate(GuildType guild, IReadOnlyDictionary<string, double> ranks, Options opt)
    {
        var reqs = new List<Req>();
        long totalTdp = 0; long totalRanks = 0;

        // skillsetName → (skill → rank), for the "N highest from each set" reqs.
        var pools = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);

        foreach (var (skill, rankD) in ranks)
        {
            var r = (int)Math.Floor(rankD);
            totalTdp   += (long)r * (r + 1) / 2;
            totalRanks += r;

            if (guild.HardReqs.TryGetValue(skill, out var hard))
            {
                reqs.Add(MakeReq(hard, (int)rankD, skill, opt.TargetCircle));
                continue;
            }
            if (guild.SoftReqs.TryGetValue(skill, out var soft))
                reqs.Add(MakeReq(soft, (int)rankD, skill, opt.TargetCircle));

            if (guild.Skillsets.TryGetValue(skill, out var setName))
            {
                if (!pools.TryGetValue(setName, out var pool))
                    pools[setName] = pool = new Dictionary<string, double>(StringComparer.Ordinal);
                pool[skill] = rankD;
            }
        }
        totalTdp /= 200;

        foreach (var n in guild.TopN)
        {
            if (!pools.TryGetValue(n.Skillset, out var pool) || pool.Count == 0)
                continue;                                  // no skill in this pool yet
            var skill = Highest(pool);
            reqs.Add(MakeReq(n, (int)pool[skill], $"{n.Name} ({skill})", opt.TargetCircle));
            pool.Remove(skill);
        }

        // sort by current circle, and drop maxed-out (circle 500) reqs from the lead.
        reqs.Sort((a, b) => opt.BottomSort
            ? b.CurrentCircle.CompareTo(a.CurrentCircle)
            : a.CurrentCircle.CompareTo(b.CurrentCircle));
        if (opt.BottomSort) while (reqs.Count > 0 && reqs[^1].Circle == 500) reqs.RemoveAt(reqs.Count - 1);
        else                while (reqs.Count > 0 && reqs[0].Circle  == 500) reqs.RemoveAt(0);

        return Render(reqs, totalTdp, totalRanks, opt);
    }

    private static Req MakeReq(ReqType req, int ranks, string name, int target)
    {
        var (circle, current, needed) = CalcReq(req.Tier, ranks, target);
        return new Req(circle, current, needed, name, ranks);
    }

    /// <summary>Faithful port of <c>CalculateReq3_0</c>: accumulate the per-circle
    /// rank cost band by band until it exceeds the skill's current ranks. Returns
    /// the next circle the skill can't yet support, the highest it currently does,
    /// and the cumulative ranks that next circle needs.</summary>
    private static (int circle, int currentCircle, int ranksNeeded) CalcReq(int[] tier, int ranks, int target)
    {
        (int from, int to, int idx)[] bands =
            { (1, 10, 0), (11, 30, 1), (31, 70, 2), (71, 100, 3), (101, 150, 4), (151, 500, 5) };

        int needed = 0, currentCircle = 0;
        foreach (var (from, to, idx) in bands)
        {
            for (int i = from; i <= to; i++)
            {
                needed += tier[idx];
                if (needed > ranks)
                {
                    if (target > i)
                    {
                        if (currentCircle == 0) currentCircle = i - 1;
                        continue;
                    }
                    if (needed - tier[idx] <= ranks) currentCircle = i - 1;
                    return (i, currentCircle, needed);
                }
            }
        }
        return (500, currentCircle, needed);
    }

    private static List<string> Render(List<Req> reqs, long totalTdp, long totalRanks, Options opt)
    {
        var outp = new List<string>();
        if (reqs.Count == 0)
        {
            outp.Add("Circle Calc: no requirements matched your skills (is the guild correct?).");
            return outp;
        }

        var bindingCircle = opt.BottomSort ? reqs[^1].Circle : reqs[0].Circle;
        outp.Add($"Requirements for Circle {bindingCircle}:");
        outp.Add("");

        var addedBreak = false;
        foreach (var r in reqs)
        {
            // blank line once we move past the binding-circle group (not in next-only mode).
            if (!addedBreak && opt.Display != 2 &&
                ((!opt.BottomSort && r.Circle != bindingCircle) ||
                 ( opt.BottomSort && r.Circle == bindingCircle)))
            {
                outp.Add("");
                addedBreak = true;
            }

            var show = opt.Display switch
            {
                2 => r.Circle == bindingCircle,
                1 => true,
                _ => r.Circle <= 200,
            };
            if (show)
                outp.Add($"You have enough {r.Name} for Circle {r.CurrentCircle} and need " +
                         $"{r.RanksNeeded - r.Ranks} ({r.RanksNeeded}) ranks for Circle {r.Circle}");
        }

        outp.Add("");
        outp.Add($"TDPs Gained: {totalTdp,6}");
        outp.Add($"Total Ranks: {totalRanks,6}");
        return outp;
    }

    // ── skill sorting ───────────────────────────────────────────────────────────

    /// <summary>Port of <c>SortSkills</c>/<c>ShowRanks</c>: list the given skills
    /// highest-rank first, then the TDP/Total-Ranks summary. <paramref name="filter"/>
    /// (a custom group's skill set) restricts which skills count, if supplied.</summary>
    public static List<string> Sort(IReadOnlyDictionary<string, double> ranks, string label,
                                    IReadOnlySet<string>? filter)
    {
        var rows = ranks
            .Where(kv => filter is null || filter.Contains(kv.Key))
            .OrderByDescending(kv => kv.Value)
            .ToList();

        long totalTdp = 0; long totalRanks = 0;
        var nameW = 0; var rankW = 0;
        foreach (var (name, rank) in rows)
        {
            var r = (int)Math.Floor(rank);
            totalTdp += (long)r * (r + 1) / 2;
            totalRanks += r;
            nameW = Math.Max(nameW, name.Length);
            rankW = Math.Max(rankW, rank.ToString("F2").Length);
        }
        totalTdp /= 200;

        var outp = new List<string> { "" };
        foreach (var (name, rank) in rows)
            outp.Add($"{name.PadRight(nameW)} - {rank.ToString("F2").PadLeft(rankW)}");
        outp.Add("");
        outp.Add($"TDPs Gained from {label}: {totalTdp,6}");
        outp.Add($"Total Ranks in {label}:   {totalRanks,6}");
        return outp;
    }

    private static string Highest(Dictionary<string, double> pool)
    {
        var best = ""; var top = double.NegativeInfinity;
        foreach (var (name, rank) in pool)
            if (rank > top) { top = rank; best = name; }
        return best;
    }
}

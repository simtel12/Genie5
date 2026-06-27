using System.Reflection;
using System.Xml.Linq;

namespace Genie.Core.Extensions.Builtin.CircleCalc;

/// <summary>One requirement row: the per-tier rank cost across the six circle
/// bands (1-10, 11-30, 31-70, 71-100, 101-150, 151+). A "Hard"/"Soft" req is
/// matched by skill name; an "N" req draws the highest-ranked skill from a named
/// skillset pool.</summary>
internal sealed class ReqType
{
    public string Name     = "";
    public string Skillset = "";          // for "N" reqs: the pool to draw from; else ""
    public readonly int[] Tier = new int[6];
}

/// <summary>A guild's full requirement set, mirroring the Genie 4 structure.</summary>
internal sealed class GuildType
{
    public readonly Dictionary<string, string>  Skillsets = new(StringComparer.Ordinal); // skill → skillset name
    public readonly Dictionary<string, ReqType> HardReqs  = new(StringComparer.Ordinal);
    public readonly Dictionary<string, ReqType> SoftReqs  = new(StringComparer.Ordinal);
    public readonly List<ReqType>               TopN      = new();                        // ordered (1st, 2nd, …)
}

/// <summary>A custom sort group from SortGroups.xml.</summary>
internal sealed class SortGroup
{
    public string Name = "";
    public string Skillset = "all";                 // "all"/"armor"/… — restricts which skills the exp dump pulls
    public readonly HashSet<string> Skills = new(StringComparer.Ordinal);
}

/// <summary>
/// Loads and holds the Circle Calculator data: guild requirements
/// (<c>CircleReqs.xml</c>) and custom sort groups (<c>SortGroups.xml</c>). Both
/// ship embedded in Genie.Core as defaults; a user copy dropped in
/// <see cref="ConfigDir"/> (the active config/profile dir) overrides. Faithful
/// port of the Genie 4 loaders, re-expressed with <see cref="XDocument"/>.
/// </summary>
internal sealed class CircleData
{
    /// <summary>Active config dir checked for a user-supplied override copy of
    /// either data file before falling back to the embedded default. Set by the
    /// extension from <c>IExtensionHost.ConfigDir</c>; empty means embedded-only.</summary>
    public string ConfigDir = "";

    // guild reqs
    public readonly Dictionary<string, string>    GuildByShort = new(StringComparer.OrdinalIgnoreCase); // shortname/name → guild name
    public readonly Dictionary<string, GuildType> Guilds       = new(StringComparer.Ordinal);
    public string ReqsVer = "0.0";
    public bool   ReqsLoaded;

    // sort groups
    public readonly Dictionary<string, SortGroup> SortByShort = new(StringComparer.OrdinalIgnoreCase);  // shortname → group
    public string SortVer = "0.0";
    public bool   SortLoaded;

    public bool TryGuild(string nameOrShort, out GuildType guild)
    {
        guild = null!;
        if (GuildByShort.TryGetValue(nameOrShort.Trim(), out var full) &&
            Guilds.TryGetValue(full, out var g)) { guild = g; return true; }
        return false;
    }

    // ── loading ─────────────────────────────────────────────────────────────────

    /// <summary>(re)load both files. <paramref name="log"/> receives status lines.</summary>
    public void LoadAll(Action<string> log)
    {
        LoadReqs(log);
        LoadSort(log);
    }

    public void LoadReqs(Action<string> log)
    {
        var xml = ReadData("CircleReqs.xml");
        if (xml is null) { log("Circle Calc: no CircleReqs.xml found — calculating disabled."); return; }
        try
        {
            var root = XDocument.Parse(xml).Root!;
            ReqsVer = (string?)root.Attribute("ver") ?? "0.0";
            GuildByShort.Clear(); Guilds.Clear();

            foreach (var ge in root.Elements("Guild"))
            {
                var gname = (string?)ge.Attribute("name") ?? "";
                if (gname.Length == 0) continue;
                var guild = new GuildType();

                GuildByShort[gname] = gname;
                foreach (var sn in ge.Elements("Shortname"))
                    GuildByShort[sn.Value.Trim()] = gname;

                foreach (var ss in ge.Elements("Skillset"))
                {
                    var setName = (string?)ss.Attribute("name") ?? "";
                    foreach (var sk in ss.Elements("Skill"))
                        guild.Skillsets[sk.Value.Trim()] = setName;
                }

                foreach (var re in ge.Elements("Req"))
                {
                    var type = (string?)re.Attribute("type") ?? "";
                    var req  = new ReqType { Name = (string?)re.Attribute("name") ?? "" };
                    if (type == "N") req.Skillset = (string?)re.Attribute("Skillset") ?? "";
                    req.Tier[0] = TierVal(re, "Circles1-10");
                    req.Tier[1] = TierVal(re, "Circles11-30");
                    req.Tier[2] = TierVal(re, "Circles31-70");
                    req.Tier[3] = TierVal(re, "Circles71-100");
                    req.Tier[4] = TierVal(re, "Circles101-150");
                    req.Tier[5] = TierVal(re, "Circles151Up");

                    if      (type == "Hard") guild.HardReqs[req.Name] = req;
                    else if (type == "Soft") guild.SoftReqs[req.Name] = req;
                    else                     guild.TopN.Add(req);
                }
                Guilds[gname] = guild;
            }
            ReqsLoaded = true;
            log($"Circle Calc: loaded requirements v{ReqsVer} ({Guilds.Count} guilds).");
        }
        catch (Exception ex) { log("Circle Calc: failed to load CircleReqs.xml — " + ex.Message); }
    }

    public void LoadSort(Action<string> log)
    {
        var xml = ReadData("SortGroups.xml");
        if (xml is null) { log("Circle Calc: no SortGroups.xml — custom sort groups disabled."); return; }
        try
        {
            var root = XDocument.Parse(xml).Root!;
            SortVer = (string?)root.Attribute("ver") ?? "0.0";
            SortByShort.Clear();

            foreach (var sg in root.Elements("SkillGroup"))
            {
                var grp = new SortGroup
                {
                    Name     = (string?)sg.Attribute("name") ?? "",
                    Skillset = ((string?)sg.Attribute("skillset") ?? "all").ToLowerInvariant(),
                };
                foreach (var sk in sg.Elements("Skill")) grp.Skills.Add(sk.Value.Trim());
                foreach (var sn in sg.Elements("Shortname")) SortByShort[sn.Value.Trim()] = grp;
            }
            SortLoaded = true;
            log($"Circle Calc: loaded sort groups v{SortVer} ({SortByShort.Values.Distinct().Count()} groups).");
        }
        catch (Exception ex) { log("Circle Calc: failed to load SortGroups.xml — " + ex.Message); }
    }

    private static int TierVal(XElement req, string name) =>
        int.TryParse(req.Element(name)?.Value, out var v) ? v : 0;

    // ── data resolution: user override first, then embedded default ──────────────

    private string? ReadData(string file)
    {
        if (!string.IsNullOrEmpty(ConfigDir))
        {
            var path = Path.Combine(ConfigDir, file);
            if (File.Exists(path))
            {
                try { return File.ReadAllText(path); } catch { /* fall through to embedded */ }
            }
        }
        var asm  = typeof(CircleData).Assembly;
        var name = asm.GetManifestResourceNames()
                      .FirstOrDefault(n => n.EndsWith(file, StringComparison.Ordinal));
        if (name is null) return null;
        using var s = asm.GetManifestResourceStream(name);
        if (s is null) return null;
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}

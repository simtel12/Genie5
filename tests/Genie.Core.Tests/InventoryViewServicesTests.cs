using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Genie.Core.Extensions.Builtin.InventoryView;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// The Inventory View data services: Elanthipedia weight/size resolution
/// (Semantic MediaWiki <c>ask</c> parsing + persistent cache) and the DR
/// Service Plaza player-shop export (minimal xlsx reader + search). Network
/// seams are faked; the xlsx fixture is generated in the exact element shape
/// of the real export (x:-prefixed OOXML, shared strings, header row).
/// </summary>
public class InventoryViewServicesTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "genie-ivsvc-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    // ── Wiki item info ──────────────────────────────────────────────────────────

    /// <summary>Shape captured verbatim from the live API (2026-07-14).</summary>
    private const string AskJson = """
        {"query":{"printrequests":[],"results":{
        "Weapon:Broad iron staff weighted with alternating panels of black and blue gold":{
          "printouts":{"Weight of":[50],"Length is":[19],"Width is":[1],"Height is":[1]},
          "fulltext":"Weapon:Broad iron staff weighted with alternating panels of black and blue gold",
          "fullurl":"https://elanthipedia.play.net/Weapon:Broad_iron_staff","namespace":114,"exists":"1"},
        "Item:Vault book":{
          "printouts":{"Weight of":[1],"Length is":[2],"Width is":[2],"Height is":[1]},
          "fulltext":"Item:Vault book",
          "fullurl":"https://elanthipedia.play.net/Item:Vault_book","namespace":118,"exists":"1"},
        "Item:Bare page":{
          "printouts":{"Weight of":[],"Length is":[],"Width is":[],"Height is":[]},
          "fulltext":"Item:Bare page",
          "fullurl":"https://elanthipedia.play.net/Item:Bare_page","namespace":118,"exists":"1"}
        },"serializer":"SMW","version":2}}
        """;

    [Fact]
    public void Ask_response_parses_weights_and_dimensions_by_title()
    {
        var found = WikiItemInfoCache.ParseAskByTitle(AskJson);
        Assert.NotNull(found);

        var book = found!["Item:Vault book"];
        Assert.Equal(1, book.Weight);
        Assert.Equal("2×2×1", book.SizeText);
        Assert.Equal("1", book.WeightText);
        Assert.Equal("https://elanthipedia.play.net/Item:Vault_book", book.PageUrl);

        var staff = found["Weapon:Broad iron staff weighted with alternating panels of black and blue gold"];
        Assert.Equal("50", staff.WeightText);
        Assert.Equal("19×1×1", staff.SizeText);

        // A page with no recorded data still parses (empty texts).
        var bare = found["Item:Bare page"];
        Assert.Equal("", bare.WeightText);
        Assert.Equal("", bare.SizeText);
    }

    [Fact]
    public void Api_error_responses_parse_to_null_never_empty()
    {
        // The wiki rejects oversized SMW queries with {"error":…} — that must
        // read as FAILURE, not as "no results" (the poisoned-cache bug).
        const string err = """{"error":{"query":["restrictions on query size or depth"]}}""";
        Assert.Null(WikiItemInfoCache.ParseAskByTitle(err));
        Assert.Null(WikiItemInfoCache.ParseExistence(err));
    }

    [Fact]
    public void Existence_response_yields_titles_and_rename_chain()
    {
        const string json = """
            {"query":{
              "normalized":[{"from":"Item:vault book","to":"Item:Vault book"}],
              "redirects":[{"from":"Item:Vault book","to":"Item:Vault ledger"}],
              "pages":{
                "71418":{"pageid":71418,"ns":118,"title":"Item:Vault ledger"},
                "-1":{"ns":114,"title":"Weapon:Vault book","missing":""}
              }}}
            """;
        var parsed = WikiItemInfoCache.ParseExistence(json);
        Assert.NotNull(parsed);
        var (existing, renames) = parsed!.Value;
        Assert.Contains("Item:Vault ledger", existing);
        Assert.DoesNotContain("Weapon:Vault book", existing);
        Assert.Equal("Item:Vault book", renames["Item:vault book"]);
        Assert.Equal("Item:Vault ledger", renames["Item:Vault book"]);
    }

    [Fact]
    public void Size_text_marks_gaps_and_weight_drops_decimal_noise()
    {
        Assert.Equal("19×?×1", new WikiItemInfo("x", null, 19, null, 1, null).SizeText);
        Assert.Equal("",       new WikiItemInfo("x", null, null, null, null, null).SizeText);
        Assert.Equal("2.5",    new WikiItemInfo("x", 2.5, null, null, null, null).WeightText);
        Assert.Equal("",      new WikiItemInfo("x", null, null, null, null, null).WeightText);
    }

    /// <summary>Existence response: Item:Vault book exists (after case
    /// normalization); every candidate for "made-up gizmo" is missing.</summary>
    private const string ExistJson = """
        {"query":{
          "normalized":[{"from":"Item:vault book","to":"Item:Vault book"}],
          "pages":{
            "71418":{"pageid":71418,"ns":118,"title":"Item:Vault book"},
            "-1":{"title":"Weapon:Vault book","missing":""},
            "-2":{"title":"Armor:Vault book","missing":""},
            "-3":{"title":"Shield:Vault book","missing":""},
            "-4":{"title":"Item:Made-up gizmo","missing":""},
            "-5":{"title":"Weapon:Made-up gizmo","missing":""},
            "-6":{"title":"Armor:Made-up gizmo","missing":""},
            "-7":{"title":"Shield:Made-up gizmo","missing":""}
          }}}
        """;

    [Fact]
    public async Task Resolve_caches_hits_and_authoritative_negatives_and_persists()
    {
        var cache = new WikiItemInfoCache(_root);
        int askCalls = 0, queryCalls = 0;
        cache.QueryApi = _ => { queryCalls++; return Task.FromResult<string?>(ExistJson); };
        cache.AskApi   = q => { askCalls++;
            Assert.DoesNotContain("Made-up gizmo", q);   // missing pages never reach ask
            return Task.FromResult<string?>(AskJson); };

        var settled = await cache.ResolveBatchAsync(new[] { "vault book", "made-up gizmo" });
        Assert.Equal(2, settled);                        // one hit + one authoritative negative
        Assert.Equal(1, queryCalls);
        Assert.Equal(1, askCalls);

        Assert.Equal("1", cache.Get("a vault book")!.WeightText);   // article-tolerant
        var negative = cache.Get("made-up gizmo");
        Assert.NotNull(negative);                        // negative IS cached…
        Assert.Equal("", negative!.WeightText);          // …with no data

        // Nothing unresolved anymore; a fresh instance reads the same file.
        Assert.Empty(cache.Unresolved(new[] { "a vault book", "some made-up gizmo" }));
        var second = new WikiItemInfoCache(_root);
        Assert.Equal("2×2×1", second.Get("vault book")!.SizeText);
        Assert.Empty(second.Unresolved(new[] { "made-up gizmo" }));
    }

    [Fact]
    public async Task Resolve_failures_cache_nothing_at_any_phase()
    {
        // Phase-1 network failure.
        var cache = new WikiItemInfoCache(_root);
        cache.QueryApi = _ => Task.FromResult<string?>(null);
        Assert.Equal(0, await cache.ResolveBatchAsync(new[] { "vault book" }));
        Assert.Null(cache.Get("vault book"));

        // Phase-1 API error (e.g. throttled).
        cache.QueryApi = _ => Task.FromResult<string?>("""{"error":{"code":"toomany"}}""");
        Assert.Equal(0, await cache.ResolveBatchAsync(new[] { "vault book" }));
        Assert.Null(cache.Get("vault book"));

        // Phase-2 SMW error: even the page-exists fact must not be cached,
        // and CRUCIALLY the missing taps must not be negative-cached.
        cache.QueryApi = _ => Task.FromResult<string?>(ExistJson);
        cache.AskApi   = _ => Task.FromResult<string?>("""{"error":{"query":["query size"]}}""");
        Assert.Equal(0, await cache.ResolveBatchAsync(new[] { "vault book", "made-up gizmo" }));
        Assert.Null(cache.Get("vault book"));
        Assert.Null(cache.Get("made-up gizmo"));
        Assert.Equal(2, cache.Unresolved(new[] { "vault book", "made-up gizmo" }).Count);
    }

    // ── Plaza shop export ───────────────────────────────────────────────────────

    /// <summary>Build an xlsx in the Plaza export's exact shape: x:-prefixed
    /// OOXML, all strings shared, first row = headers.</summary>
    private static byte[] MakePlazaXlsx(string[][] rows)
    {
        var strings = new List<string>();
        int Shared(string s)
        {
            int i = strings.IndexOf(s);
            if (i < 0) { strings.Add(s); i = strings.Count - 1; }
            return i;
        }

        var sheetRows = new StringBuilder();
        for (int r = 0; r < rows.Length; r++)
        {
            sheetRows.Append($"<x:row r=\"{r + 1}\">");
            for (int c = 0; c < rows[r].Length; c++)
            {
                var col = (char)('A' + c);
                sheetRows.Append($"<x:c r=\"{col}{r + 1}\" t=\"s\"><x:v>{Shared(rows[r][c])}</x:v></x:c>");
            }
            sheetRows.Append("</x:row>");
        }

        const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sst = new StringBuilder($"<x:sst xmlns:x=\"{ns}\">");
        foreach (var s in strings)
            sst.Append($"<x:si><x:t>{System.Security.SecurityElement.Escape(s)}</x:t></x:si>");
        sst.Append("</x:sst>");

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, string content)
            {
                using var w = new StreamWriter(zip.CreateEntry(path).Open());
                w.Write(content);
            }
            Add("xl/sharedStrings.xml", sst.ToString());
            Add("xl/worksheets/sheet.xml",
                $"<x:worksheet xmlns:x=\"{ns}\"><x:sheetData>{sheetRows}</x:sheetData></x:worksheet>");
        }
        return ms.ToArray();
    }

    private static readonly string[] PlazaHeader =
    {
        "Tap", "Look", "Worn", "Read", "Price", "Plats", "KValue", "Surface",
        "Shop Name", "Owner", "Open", "Close", "Room", "City", "ePediaName", "ScanDate",
    };

    [Fact]
    public async Task Plaza_search_matches_tap_and_look_and_caps()
    {
        var xlsx = MakePlazaXlsx(new[]
        {
            PlazaHeader,
            new[] { "carved oaken parry stick", "A sturdy stick.", "", "", "5000 Kronars", "0", "5", "a table",
                    "Sticks R Us", "Secretia", "9am", "1am", "Front Room", "Crossing", "", "6/1/2026" },
            new[] { "iron war hammer", "Reminds you of a parry stick somehow.", "", "", "2 plat", "2", "9", "a shelf",
                    "Hammer Time", "Bob", "", "", "Back Room", "Riverhaven", "", "6/1/2026" },
            new[] { "silk gown", "Very fancy.", "", "", "1 plat", "1", "2", "a rack",
                    "Fancy Things", "Alice", "", "", "Main Room", "Crossing", "", "6/1/2026" },
        });

        var svc = new PlazaShopService(_root);
        svc.Download = () => Task.FromResult<byte[]?>(xlsx);

        var hits = await svc.SearchAsync("parry stick");
        Assert.NotNull(hits);
        Assert.Equal(2, hits!.Count);                     // tap match + look match
        var stick = hits.Single(h => h.Tap == "carved oaken parry stick");
        Assert.Equal("5000 Kronars", stick.Price);
        Assert.Equal("Sticks R Us", stick.Shop);
        Assert.Equal("Crossing", stick.City);
        Assert.Equal("Front Room", stick.Room);

        Assert.Empty((await svc.SearchAsync("no such thing"))!);
        var capped = await svc.SearchAsync("a", cap: 1);
        Assert.Single(capped!);
    }

    [Fact]
    public async Task Plaza_offline_with_cold_cache_returns_null_then_uses_disk_cache()
    {
        var offline = new PlazaShopService(_root);
        offline.Download = () => Task.FromResult<byte[]?>(null);
        Assert.Null(await offline.SearchAsync("anything"));

        // A successful fetch writes the disk cache…
        var xlsx = MakePlazaXlsx(new[]
        {
            PlazaHeader,
            new[] { "ruby ring", "Shiny.", "", "", "1 plat", "1", "1", "a case",
                    "Gems", "Eve", "", "", "Room", "Shard", "", "6/1/2026" },
        });
        var online = new PlazaShopService(_root);
        online.Download = () => Task.FromResult<byte[]?>(xlsx);
        Assert.Single((await online.SearchAsync("ruby"))!);

        // …which a later offline instance can serve (fresh TTL → no download).
        var later = new PlazaShopService(_root);
        later.Download = () => Task.FromResult<byte[]?>(null);
        var hits = await later.SearchAsync("ruby");
        Assert.NotNull(hits);
        Assert.Single(hits!);
    }

    [Fact]
    public void Plaza_columns_resolve_by_header_name_not_position()
    {
        // Same data with Tap and City swapped into different columns.
        var xlsx = MakePlazaXlsx(new[]
        {
            new[] { "City", "Price", "Tap", "Shop Name" },
            new[] { "Shard", "9 plat", "obsidian dagger", "Knives" },
        });
        var listings = PlazaShopService.ParseXlsx(new MemoryStream(xlsx));
        var l = Assert.Single(listings);
        Assert.Equal("obsidian dagger", l.Tap);
        Assert.Equal("Shard", l.City);
        Assert.Equal("9 plat", l.Price);
        Assert.Equal("Knives", l.Shop);
        Assert.Equal("", l.Room);                         // absent column → empty
    }

    [Fact]
    public void Plaza_parses_the_real_export_when_available()
    {
        // Integration check against the actual downloaded export, when the
        // session scratchpad copy exists (skipped silently elsewhere/CI).
        var real = @"C:\Users\TSR-JA~1\AppData\Local\Temp\claude\D--Genie5Project\355f7874-d746-400f-91df-93c3b680b61d\scratchpad\plaza\latest.xlsx";
        if (!File.Exists(real)) return;

        using var fs = File.OpenRead(real);
        var listings = PlazaShopService.ParseXlsx(fs);
        Assert.True(listings.Count > 1000, $"expected thousands of listings, got {listings.Count}");
        Assert.All(listings.Take(50), l => Assert.False(string.IsNullOrEmpty(l.Tap)));
        Assert.Contains(listings, l => !string.IsNullOrEmpty(l.City));
    }
}

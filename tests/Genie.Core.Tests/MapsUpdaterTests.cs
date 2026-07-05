using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Genie.Core.Mapper;
using Genie.Core.Update.Sources;
using Genie.Core.Update.Updaters;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// MapsUpdater — unlike scripts, applied map files are re-serializations of
/// upstream (import → merge server_ids → export), so freshness is tracked via
/// the .map-shas.json manifest of applied UPSTREAM shas, never by hashing the
/// local file. These tests pin the regression where every applied zone counted
/// as "changed" forever and the status bar showed "Updates available: Maps"
/// on every launch.
/// </summary>
public class MapsUpdaterTests : IDisposable
{
    private readonly string _dir;
    private readonly MapZoneRepository _repo = new();

    public MapsUpdaterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "genie5-maps-updater-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static string ZoneXml(string name, string roomTitle) =>
        $"<zone name=\"{name}\" id=\"1\">" +
        $"<node id=\"1\" name=\"{roomTitle}\"><position x=\"20\" y=\"20\" z=\"0\" /></node>" +
        "</zone>";

    /// <summary>In-memory IFileListSource: name → content, with real git blob shas. Mutable so tests can simulate an upstream push.</summary>
    private sealed class FakeSource : IFileListSource
    {
        private readonly Dictionary<string, byte[]> _files;
        public int Downloads { get; private set; }
        public string Description => "fake/maps";

        public FakeSource(params (string Name, string Content)[] files)
        {
            _files = files.ToDictionary(f => f.Name, f => Encoding.UTF8.GetBytes(f.Content));
        }

        public void SetContent(string name, string content)
            => _files[name] = Encoding.UTF8.GetBytes(content);

        public Task<FileListInfo> GetFileListAsync(CancellationToken ct = default) =>
            Task.FromResult(new FileListInfo(Description, _files
                .Select(kv => new FileEntry(kv.Key, "mem://" + kv.Key, BlobSha(kv.Value), kv.Value.Length))
                .ToList()));

        public Task<byte[]> DownloadFileAsync(FileEntry file, CancellationToken ct = default)
        {
            Downloads++;
            return Task.FromResult(_files[file.Name]);
        }

        private static string BlobSha(byte[] content)
        {
            var header = Encoding.ASCII.GetBytes($"blob {content.Length}\0");
            using var sha = System.Security.Cryptography.SHA1.Create();
            sha.TransformBlock(header, 0, header.Length, null, 0);
            sha.TransformFinalBlock(content, 0, content.Length);
            return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        }
    }

    private MapsUpdater NewUpdater(FakeSource source)
        => new(_repo, _dir, new IFileListSource[] { source });

    [Fact]
    public async Task Check_counts_missing_zones_as_new_without_downloading()
    {
        var source  = new FakeSource(("MapTest1.xml", ZoneXml("Zone A", "Room A")));
        var updater = NewUpdater(source);

        var check = await updater.CheckAsync();

        Assert.True(check.UpdateAvailable);
        Assert.Contains("1 new", check.LatestVersion);
        Assert.Equal(0, source.Downloads);
    }

    [Fact]
    public async Task Check_reports_up_to_date_after_apply_despite_reserialized_local_file()
    {
        var source  = new FakeSource(("MapTest1.xml", ZoneXml("Zone A", "Room A")));
        var updater = NewUpdater(source);

        var apply = await updater.ApplyAsync();
        Assert.True(apply.Succeeded);

        // The applied file is our own serialization, NOT the upstream bytes —
        // exactly the condition that made the old local-hash check misfire.
        var localBytes    = File.ReadAllBytes(Path.Combine(_dir, "MapTest1.xml"));
        var upstreamBytes = Encoding.UTF8.GetBytes(ZoneXml("Zone A", "Room A"));
        Assert.NotEqual(upstreamBytes, localBytes);

        var check = await updater.CheckAsync();
        Assert.False(check.UpdateAvailable);
        Assert.Equal("Up to date", check.LatestVersion);
    }

    [Fact]
    public async Task Apply_skips_current_zones_without_downloading()
    {
        var source  = new FakeSource(
            ("MapTest1.xml", ZoneXml("Zone A", "Room A")),
            ("MapTest2.xml", ZoneXml("Zone B", "Room B")));
        var updater = NewUpdater(source);

        await updater.ApplyAsync();
        Assert.Equal(2, source.Downloads);

        var second = await updater.ApplyAsync();
        Assert.True(second.Succeeded);
        Assert.Equal(2, source.Downloads);
        Assert.Contains("2 already current", second.Summary);
    }

    [Fact]
    public async Task Upstream_change_is_flagged_then_goes_quiet_after_reapply()
    {
        var source  = new FakeSource(("MapTest1.xml", ZoneXml("Zone A", "Room A")));
        var updater = NewUpdater(source);
        await updater.ApplyAsync();

        source.SetContent("MapTest1.xml", ZoneXml("Zone A", "Room A Renamed"));

        var check = await updater.CheckAsync();
        Assert.True(check.UpdateAvailable);
        Assert.Contains("1 changed", check.LatestVersion);

        var apply = await updater.ApplyAsync();
        Assert.True(apply.Succeeded);
        Assert.Contains("1 merged", apply.Summary);

        var after = await updater.CheckAsync();
        Assert.False(after.UpdateAvailable);
    }

    [Fact]
    public async Task Reapply_preserves_locally_collected_server_ids()
    {
        var source  = new FakeSource(("MapTest1.xml", ZoneXml("Zone A", "Room A")));
        var updater = NewUpdater(source);
        await updater.ApplyAsync();

        // Simulate the player visiting the room: server room id collected locally.
        var localPath = Path.Combine(_dir, "MapTest1.xml");
        var zone = _repo.Load(localPath)!;
        zone.Nodes[1].ServerRoomId = "12345";
        _repo.Save(localPath, zone);

        source.SetContent("MapTest1.xml", ZoneXml("Zone A", "Room A Renamed"));
        var apply = await updater.ApplyAsync();
        Assert.True(apply.Succeeded);

        var merged = _repo.Load(localPath)!;
        Assert.Equal("12345", merged.Nodes[1].ServerRoomId);
        Assert.Equal("Room A Renamed", merged.Nodes[1].Title);
    }

    [Fact]
    public async Task Zone_with_no_recorded_sha_counts_as_changed_once_then_recovers()
    {
        // Pre-manifest install: the zone file exists but .map-shas.json doesn't.
        var source  = new FakeSource(("MapTest1.xml", ZoneXml("Zone A", "Room A")));
        Directory.CreateDirectory(_dir);
        _repo.Save(Path.Combine(_dir, "MapTest1.xml"),
                   Genie4MapImporter.ImportFromContent(ZoneXml("Zone A", "Room A"), "MapTest1"));

        var updater = NewUpdater(source);
        var before  = await updater.CheckAsync();
        Assert.True(before.UpdateAvailable);
        Assert.Contains("1 changed", before.LatestVersion);

        // One apply records the upstream sha; the banner never returns.
        await updater.ApplyAsync();
        var after = await updater.CheckAsync();
        Assert.False(after.UpdateAvailable);
    }

    [Fact]
    public async Task Manifest_file_is_not_listed_as_a_zone()
    {
        var source  = new FakeSource(("MapTest1.xml", ZoneXml("Zone A", "Room A")));
        var updater = NewUpdater(source);
        await updater.ApplyAsync();

        Assert.True(File.Exists(Path.Combine(_dir, ".map-shas.json")));
        var zoneFiles = _repo.ListZoneFiles(_dir);
        Assert.Single(zoneFiles);
        Assert.EndsWith("MapTest1.xml", zoneFiles[0]);
    }
}

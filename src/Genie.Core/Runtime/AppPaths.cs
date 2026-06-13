using System.Runtime.InteropServices;

namespace Genie.Core.Runtime;

public sealed class AppPaths
{
    public AppPaths(string basePath, bool isLocal)
    {
        BasePath = Path.GetFullPath(basePath);
        IsLocal = isLocal;
    }

    public string BasePath { get; }
    public bool IsLocal { get; }

    /// <summary>
    /// Sentinel file that, when present next to the executable, puts Genie in
    /// portable mode: all data folders (Config, Scripts, Maps, Plugins, Logs,
    /// Profiles, Layouts, …) live beside the exe instead of in the per-user
    /// data directory. The portable .zip ships with this file; the installer
    /// does not. Contents are ignored — only its presence matters. Note that
    /// since the move to portable-first discovery, a plain <c>Config</c> folder
    /// beside the exe is enough on its own — this marker is no longer required,
    /// only still honored for back-compat.
    /// </summary>
    public const string PortableMarkerFileName = "genie5.portable";

    /// <summary>
    /// Resolve the data root, <b>portable-first</b>: local (beside the exe)
    /// always wins when it holds Genie data, then the per-user OS folder, and
    /// only if neither exists do we fall back to creating a local install.
    ///
    /// "Holds Genie data" means a <c>Config</c> folder (or the legacy
    /// <see cref="PortableMarkerFileName"/> marker) is present — see
    /// <see cref="HasData"/>. Fresh installs are normally resolved by the App's
    /// first-run prompt, which materializes the chosen location (local vs user
    /// folder) <i>before</i> this runs; this method's own fresh-install branch
    /// is the headless / no-UI fallback and stays portable-first.
    /// </summary>
    public static AppPaths Discover(string appName, string baseDirectory)
    {
        var userDir = GetUserDataDirectory(appName);

        // 1. Portable-first: data living beside the executable always wins. A
        //    copy of Genie with a Config folder (or the legacy portable marker)
        //    next to it runs fully local — nothing is read from or written to
        //    the per-user folder, even if that folder also holds data.
        //    The write-guard keeps a read-only drop (e.g. Program Files) from
        //    claiming portable mode and then failing to save.
        if (HasData(baseDirectory) && IsDirectoryWritable(baseDirectory))
            return new AppPaths(baseDirectory, isLocal: true);

        // 2. Otherwise use the per-user OS data folder if it already holds data.
        if (HasData(userDir))
            return new AppPaths(userDir, isLocal: false);

        // 3. Nothing anywhere yet (headless run, tests, or the first-run prompt
        //    was skipped). Stay portable-first: keep data beside the exe when we
        //    can write there, else fall back to the per-user folder.
        if (IsDirectoryWritable(baseDirectory))
            return new AppPaths(baseDirectory, isLocal: true);

        Directory.CreateDirectory(userDir);
        return new AppPaths(userDir, isLocal: false);
    }

    /// <summary>
    /// The per-user OS data directory for <paramref name="appName"/> — the
    /// non-portable location. Pure path computation, no side effects:
    /// <list type="bullet">
    /// <item>Windows: <c>%APPDATA%\AppName</c></item>
    /// <item>macOS: <c>~/Library/Application Support/AppName</c></item>
    /// <item>Linux/Unix: <c>$XDG_DATA_HOME/AppName</c> (fallback <c>~/.local/share/AppName</c>)</item>
    /// </list>
    /// </summary>
    public static string GetUserDataDirectory(string appName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(roaming, appName);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", appName);
        }

        // Linux / other Unix — respect XDG_DATA_HOME
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(xdg))
            xdg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return Path.Combine(xdg, appName);
    }

    /// <summary>
    /// True if <paramref name="dir"/> already holds a Genie install — i.e. it
    /// has a <c>Config</c> folder or the legacy <see cref="PortableMarkerFileName"/>
    /// marker. This is the signal both the portable-first <see cref="Discover"/>
    /// and the App's first-run prompt use to tell "already set up here" from
    /// "fresh".
    /// </summary>
    public static bool HasData(string dir)
        => File.Exists(Path.Combine(dir, PortableMarkerFileName))
        || Directory.Exists(Path.Combine(dir, "Config"));

    /// <summary>
    /// True if we can create/delete a file in <paramref name="dir"/>. Used to
    /// decide whether portable mode is actually viable at this location (e.g.
    /// the first-run prompt offers the local option only when it is writable).
    /// </summary>
    public static bool IsDirectoryWritable(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, ".genie5-write-probe");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string ResolvePath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return BasePath;
        if (Path.IsPathRooted(configuredPath))
            return Path.GetFullPath(configuredPath);
        return Path.GetFullPath(Path.Combine(BasePath, configuredPath));
    }

    public string ValidateDirectory(string configuredPath)
    {
        var fullPath = ResolvePath(configuredPath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }
}

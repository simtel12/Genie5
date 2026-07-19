using System.Diagnostics;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Genie.Core.Connection;

/// <summary>What <see cref="LichLauncher.EnsureRunningAsync"/> did.</summary>
public enum LichLaunchOutcome
{
    /// <summary>The proxy port was already listening — a Lich (or other proxy)
    /// is already up, so nothing was launched and the caller should just connect.</summary>
    AlreadyRunning,

    /// <summary>Lich was started and its proxy port came up within the wait window.</summary>
    Launched,

    /// <summary>Auto-launch was requested but could not complete — bad/missing
    /// paths, the process failed to start, or the port never opened. The caller
    /// should surface <see cref="LichLaunchResult.Message"/> and NOT connect.</summary>
    Failed,
}

/// <summary>Outcome + a human-readable message for the game window.
/// <see cref="ProcessId"/> is set only when <see cref="Outcome"/> is
/// <see cref="LichLaunchOutcome.Launched"/> — the caller owns that process and
/// should stop it on disconnect / character change via <see cref="LichLauncher.TryStop"/>.</summary>
public sealed record LichLaunchResult(LichLaunchOutcome Outcome, string Message, int? ProcessId = null);

/// <summary>
/// Cross-platform "start Lich for me" helper backing the Genie 4 <c>#lc</c> /
/// <c>#lconnect</c> / <c>#lichconnect</c> auto-launch behaviour. Unlike Genie 4
/// (Windows-only <c>cmd /C ruby lich.rbw</c> that blindly launches then sleeps a
/// fixed number of seconds), this:
/// <list type="bullet">
///   <item>is <b>idempotent</b> — if the proxy port is already listening it
///     attaches without launching, so a manually-started Lich is respected;</item>
///   <item>launches <c>ruby</c> directly (no <c>cmd.exe</c>), so it works the same
///     on Windows, macOS and Linux;</item>
///   <item><b>polls</b> the port and returns as soon as Lich is up, using the
///     start-pause only as an upper bound;</item>
///   <item>returns the launched process id so the App can stop <b>only</b> a
///     Genie-started Lich on disconnect / character change (never a manually
///     started one that was merely attached to).</item>
/// </list>
/// Pure Core code (no UI dependency); the App calls it from its connect path when
/// <see cref="Config.GenieConfig.LichAutoLaunch"/> is on and the mode is
/// <see cref="ConnectionMode.LichProxy"/>.
/// </summary>
public static class LichLauncher
{
    private static readonly Regex CharacterPlaceholder =
        new(@"\{character\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PortPlaceholder =
        new(@"\{port\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Expands <c>{character}</c> and <c>{port}</c> placeholders in a
    /// <c>#config lichargs</c> template so auto-launch can take the Character /
    /// port from the Genie Lich-proxy profile (or Connect dialog) instead of a
    /// hard-coded <c>--login</c> name.
    /// </summary>
    /// <remarks>
    /// Placeholders are case-insensitive. Static <c>lichargs</c> with no
    /// placeholders are returned unchanged (Genie 4 parity). If the template
    /// contains <c>{character}</c> and <paramref name="characterName"/> is
    /// empty, expansion fails so the caller can abort before launching Lich
    /// with a broken command line.
    /// </remarks>
    /// <param name="template">Raw <see cref="Config.GenieConfig.LichArguments"/> value.</param>
    /// <param name="characterName">Profile / Connect-dialog Character field.</param>
    /// <param name="port">Lich proxy port (<see cref="ConnectionConfig.LichProxyPort"/>).</param>
    /// <param name="expanded">Resolved argument string on success; empty on failure.</param>
    /// <param name="error">Human-readable <c>[lich] …</c> message on failure; empty on success.</param>
    /// <returns><see langword="true"/> when <paramref name="expanded"/> is safe to pass to
    /// <see cref="EnsureRunningAsync"/>.</returns>
    public static bool TryExpandArguments(
        string? template,
        string? characterName,
        int port,
        out string expanded,
        out string error)
    {
        var args = template ?? string.Empty;
        var needsCharacter = CharacterPlaceholder.IsMatch(args);
        if (needsCharacter && string.IsNullOrWhiteSpace(characterName))
        {
            expanded = string.Empty;
            error =
                "[lich] lichargs uses {character} but no Character is set on this Lich-proxy " +
                "connect. Set Character in the Connect dialog / profile, or replace {character} " +
                "with a fixed --login name in #config lichargs.";
            return false;
        }

        if (needsCharacter)
            args = CharacterPlaceholder.Replace(args, characterName!.Trim());

        args = PortPlaceholder.Replace(args, port.ToString());

        expanded = args;
        error = string.Empty;
        return true;
    }

    public static async Task<LichLaunchResult> EnsureRunningAsync(
        string host,
        int port,
        string rubyPath,
        string lichPath,
        string arguments,
        int startPauseSeconds,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        // 1. Already listening? Attach — never double-launch. This preserves the
        //    long-standing "start Lich yourself, then #lichconnect" workflow even
        //    with auto-launch turned on. No ProcessId: caller must not claim ownership.
        if (await IsPortOpenAsync(host, port, TimeSpan.FromMilliseconds(600), ct).ConfigureAwait(false))
            return new(LichLaunchOutcome.AlreadyRunning,
                $"[lich] already listening on {host}:{port} — attaching.");

        // 2. Validate the Lich script path. Ruby may be left blank to use PATH.
        if (string.IsNullOrWhiteSpace(lichPath) || !File.Exists(lichPath))
            return new(LichLaunchOutcome.Failed,
                $"[lich] auto-launch is on but no Lich script was found at '{lichPath}'. " +
                "Set it with #config lichpath {path} (and #config lichruby / lichargs), " +
                "or start Lich yourself.");

        var ruby = string.IsNullOrWhiteSpace(rubyPath) ? DefaultRubyExecutable : rubyPath;
        if (!string.IsNullOrWhiteSpace(rubyPath) && !File.Exists(rubyPath))
            return new(LichLaunchOutcome.Failed,
                $"[lich] Ruby not found at '{rubyPath}'. Fix #config lichruby, or clear it to use Ruby from PATH.");

        // 3. Launch ruby <lichPath> <arguments> and keep the PID for ownership.
        //    Progress is best-effort and must NEVER abort the launch — callers often
        //    push lines into a UI ObservableCollection, and this method uses
        //    ConfigureAwait(false), so progress may run off the UI thread and race
        //    CollectionChanged (character change: "disconnected" + relaunch).
        int pid;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName         = ruby,
                UseShellExecute  = false,
                CreateNoWindow   = true,
                WorkingDirectory = Path.GetDirectoryName(lichPath) ?? string.Empty,
            };
            psi.ArgumentList.Add(lichPath);
            foreach (var a in SplitArguments(arguments))
                psi.ArgumentList.Add(a);

            using var proc = Process.Start(psi);
            if (proc is null)
                return new(LichLaunchOutcome.Failed, "[lich] failed to start the Lich process.");
            pid = proc.Id;
        }
        catch (Exception ex)
        {
            return new(LichLaunchOutcome.Failed, $"[lich] failed to start Lich: {ex.Message}");
        }

        ReportProgress(progress, $"[lich] starting: {ruby} {lichPath} {arguments}".TrimEnd());

        // 4. Poll the proxy port until it opens or the start-pause elapses.
        //    If we never see the port, kill the orphan we just started.
        var pause = Math.Clamp(startPauseSeconds, 1, 120);
        for (var i = 0; i < pause; i++)
        {
            if (ct.IsCancellationRequested)
            {
                TryStop(pid, out _);
                return new(LichLaunchOutcome.Failed, "[lich] launch cancelled.");
            }
            if (await IsPortOpenAsync(host, port, TimeSpan.FromMilliseconds(800), ct).ConfigureAwait(false))
                return new(LichLaunchOutcome.Launched, $"[lich] up on {host}:{port}.", pid);
            ReportProgress(progress, $"[lich] waiting for Lich… ({i + 1}/{pause}s)");
            try { await Task.Delay(1000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                TryStop(pid, out _);
                return new(LichLaunchOutcome.Failed, "[lich] launch cancelled.");
            }
        }

        TryStop(pid, out _);
        return new(LichLaunchOutcome.Failed,
            $"[lich] Lich did not open {host}:{port} within {pause}s. " +
            "Increase #config lichstartpause, or check Lich's own window for errors.");
    }

    /// <summary>
    /// Stops a process Genie previously auto-launched. Safe if the PID is already
    /// gone (treated as success). Uses the entire process tree so a <c>ruby</c>
    /// wrapper and its Lich children both exit.
    /// </summary>
    /// <param name="processId">PID from <see cref="LichLaunchResult.ProcessId"/>.</param>
    /// <param name="message"><c>[lich] …</c> status line for the game window.</param>
    /// <returns><see langword="true"/> when the process is gone (killed or already exited);
    /// <see langword="false"/> only when a kill attempt threw.</returns>
    public static bool TryStop(int processId, out string message)
    {
        if (processId <= 0)
        {
            message = string.Empty;
            return false;
        }

        try
        {
            Process proc;
            try { proc = Process.GetProcessById(processId); }
            catch (ArgumentException)
            {
                message = $"[lich] auto-launched process {processId} already exited.";
                return true;
            }

            using (proc)
            {
                if (proc.HasExited)
                {
                    message = $"[lich] auto-launched process {processId} already exited.";
                    return true;
                }

                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(5_000);
                message = $"[lich] stopped auto-launched process {processId}.";
                return true;
            }
        }
        catch (Exception ex)
        {
            message = $"[lich] failed to stop process {processId}: {ex.Message}";
            return false;
        }
    }

    /// <summary>Invoke <paramref name="progress"/> without letting UI/logging
    /// failures fail the launch. See <see cref="EnsureRunningAsync"/>.</summary>
    private static void ReportProgress(Action<string>? progress, string message)
    {
        if (progress is null) return;
        try { progress(message); }
        catch { /* best-effort */ }
    }

    /// <summary>Ruby executable name when none is configured — resolved off PATH
    /// by the OS. <c>ruby</c> works on all three platforms (Windows appends
    /// <c>.exe</c> automatically under CreateProcess).</summary>
    private static string DefaultRubyExecutable => "ruby";

    /// <summary>Split a Lich argument string into individual tokens on whitespace.
    /// Lich arguments are simple flags (<c>--login X --without-frontend
    /// --detachable-client=8000</c>), so a whitespace split is sufficient;
    /// quoting is intentionally not supported (a single config value rarely needs
    /// it, and it keeps behaviour predictable).</summary>
    private static IEnumerable<string> SplitArguments(string arguments) =>
        string.IsNullOrWhiteSpace(arguments)
            ? Array.Empty<string>()
            : arguments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>True if a TCP connection to <paramref name="host"/>:<paramref name="port"/>
    /// succeeds within <paramref name="timeout"/> — used to detect a listening Lich.</summary>
    private static async Task<bool> IsPortOpenAsync(string host, int port, TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            using var tcp = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            await tcp.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
            return tcp.Connected;
        }
        catch
        {
            return false;   // refused / timed out / unreachable → not listening
        }
    }
}

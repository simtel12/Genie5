using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Genie.Core.Connection;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Bind-based FE port probe + owned-PID liveness — fixes auto-reconnect stealing
/// Lich's single accept and double-launch ownership clobber.
/// </summary>
public class LichPortProbeTests
{
    [Fact]
    public void IsProxyPortInUse_false_when_nothing_listens()
    {
        var port = FreeTcpPort();
        Assert.False(LichLauncher.IsProxyPortInUse("127.0.0.1", port));
    }

    [Fact]
    public void IsProxyPortInUse_true_when_listener_holds_port()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Assert.True(LichLauncher.IsProxyPortInUse("127.0.0.1", port));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task IsProxyPortInUse_does_not_consume_single_accept()
    {
        // Lich closes the listen socket after one accept. A connect-based probe
        // would steal that accept; the bind probe must leave it available.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Assert.True(LichLauncher.IsProxyPortInUse("127.0.0.1", port));

            var acceptTask = listener.AcceptTcpClientAsync();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var accepted = await acceptTask.WaitAsync(TimeSpan.FromSeconds(3));

            Assert.True(client.Connected);
            Assert.NotNull(accepted);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void TryIsProcessAlive_false_for_non_positive_and_missing_pids()
    {
        Assert.False(LichLauncher.TryIsProcessAlive(0));
        Assert.False(LichLauncher.TryIsProcessAlive(-1));
        Assert.False(LichLauncher.TryIsProcessAlive(int.MaxValue)); // almost certainly missing
    }

    [Fact]
    public void TryIsProcessAlive_true_for_running_process()
    {
        using var proc = StartLongLivedProcess();
        Assert.NotNull(proc);
        Assert.False(proc!.HasExited);

        Assert.True(LichLauncher.TryIsProcessAlive(proc.Id));

        LichLauncher.TryStop(proc.Id, out _);
        proc.WaitForExit(5_000);
        Assert.False(LichLauncher.TryIsProcessAlive(proc.Id));
    }

    private static int FreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static Process? StartLongLivedProcess()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Process.Start(new ProcessStartInfo
            {
                FileName        = "cmd.exe",
                Arguments       = "/c ping -n 60 127.0.0.1 >NUL",
                UseShellExecute = false,
                CreateNoWindow  = true,
            });

        return Process.Start(new ProcessStartInfo
        {
            FileName        = "/bin/sleep",
            Arguments       = "60",
            UseShellExecute = false,
            CreateNoWindow  = true,
        });
    }
}

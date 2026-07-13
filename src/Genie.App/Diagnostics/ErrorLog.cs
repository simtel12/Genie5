using System.IO;

namespace Genie.App.Diagnostics;

/// <summary>
/// Lightweight file logger for unhandled exceptions. Writes to
/// <c>Config/genie_error.log</c> alongside the other config files.
/// Existing log lines are preserved so a sequence of failures can be reviewed.
/// </summary>
public static class ErrorLog
{
    public static string? Path { get; private set; }

    public static void Initialize(string configDir)
    {
        Path = System.IO.Path.Combine(configDir, "genie_error.log");
    }

    /// <summary>Plain-text diagnostic note (no exception) — for tracing rare
    /// user-visible misbehaviors that don't throw, e.g. window-menu Copy
    /// resolving an unexpected selection.</summary>
    public static void Note(string source, string message)
    {
        if (Path is null) return;
        try
        {
            File.AppendAllText(Path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {message}\n");
        }
        catch
        {
            // Best-effort — never let the logger itself crash the app.
        }
    }

    public static void Log(string source, Exception ex)
    {
        if (Path is null) return;
        try
        {
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] " +
                        $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
            File.AppendAllText(Path, entry);
        }
        catch
        {
            // Best-effort — never let the logger itself crash the app.
        }
    }
}

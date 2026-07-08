using System.Diagnostics;
using System.IO;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Genie.App.Views;

/// <summary>
/// Help ▸ Changelog (#155). Self-contained (no view-model): shows the release
/// notes bundled with the build (the embedded <c>RELEASE_NOTES.md</c>) so users
/// can read what changed without leaving the app for the GitHub Releases page.
/// </summary>
public partial class ChangelogDialog : Window
{
    public ChangelogDialog()
    {
        InitializeComponent();
        HeaderText.Text = $"Genie {Genie.Core.GenieCore.HostVersionString} — Changelog";
        NotesText.Text  = LoadNotes();
    }

    /// <summary>Read the embedded release notes (bundled at build time from the
    /// repo-root RELEASE_NOTES.md). Best-effort — a missing resource just shows a
    /// short fallback rather than throwing.</summary>
    private static string LoadNotes()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("Genie.App.RELEASE_NOTES.md");
            if (stream is null) return "Release notes are unavailable in this build.";
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch
        {
            return "Release notes could not be loaded.";
        }
    }

    private static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* best effort — a missing browser shouldn't crash the dialog */ }
    }

    private void OnReleases(object? sender, RoutedEventArgs e)
        => Open("https://github.com/GenieClient/Genie5/releases");

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}

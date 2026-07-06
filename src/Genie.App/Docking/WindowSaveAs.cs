using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Genie.App.Docking;

/// <summary>
/// "Save As…" for the window right-click menu (#120): exports a window's
/// buffered lines to a plain-text file via the OS save dialog. Same
/// plain-text flattening as Copy All (each line's display text, no
/// colours/inlines), same TopLevel resolution as <see cref="WindowClipboard"/>.
/// Best-effort: a cancelled picker or failed write never disrupts play.
/// </summary>
internal static class WindowSaveAs
{
    public static async Task SaveLinesAsync(string windowTitle, IEnumerable<string> lines)
    {
        try
        {
            var main = (Application.Current?.ApplicationLifetime
                            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (main is null) return;

            // Window title → filesystem-safe suggested name.
            var safe = string.Join("_",
                windowTitle.Split(System.IO.Path.GetInvalidFileNameChars(),
                                  StringSplitOptions.RemoveEmptyEntries));
            if (safe.Length == 0) safe = "window";

            var file = await main.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title                  = $"Save {windowTitle} As",
                SuggestedFileName      = $"{safe}.txt",
                DefaultExtension       = "txt",
                ShowOverwritePrompt    = true,
                FileTypeChoices        = new[]
                {
                    new FilePickerFileType("Text file") { Patterns = new[] { "*.txt" } },
                    new FilePickerFileType("All files") { Patterns = new[] { "*" } },
                },
            });
            if (file is null) return;   // cancelled

            var sb = new StringBuilder();
            foreach (var line in lines)
                sb.AppendLine(line);

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new System.IO.StreamWriter(stream);
            await writer.WriteAsync(sb.ToString());
        }
        catch (Exception ex)
        {
            Diagnostics.ErrorLog.Log("WindowSaveAs.SaveLinesAsync", ex);
        }
    }
}

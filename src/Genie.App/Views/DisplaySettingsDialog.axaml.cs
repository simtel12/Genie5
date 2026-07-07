using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using Genie.App.ViewModels;
using ReactiveUI;

namespace Genie.App.Views;

public partial class DisplaySettingsDialog : ReactiveWindow<DisplaySettingsViewModel>
{
    private bool _okClosed;

    public DisplaySettingsDialog()
    {
        InitializeComponent();
        this.WhenActivated(d =>
        {
            d(ViewModel!.OkCommand    .Subscribe(result => { _okClosed = result; Close(result); }));
            d(ViewModel!.CancelCommand.Subscribe(result => Close(result)));
        });
        // Cancel AND ✕ both land here: put back the theme that was active
        // when the dialog opened (no-op if the Theme tab was never touched).
        Closed += (_, _) => { if (!_okClosed) ViewModel?.RestoreOriginalTheme(); };
    }

    // ── Theme tab (#20): pickers + name prompt are view concerns ──────────

    private static readonly FilePickerFileType ThemeJson =
        new("Genie theme") { Patterns = ["*.json"] };

    private async void OnImportThemeClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title          = "Import Theme",
            AllowMultiple  = false,
            FileTypeFilter = [ThemeJson],
        });
        if (files.Count == 0) return;
        if (files[0].TryGetLocalPath() is { } path) vm.ImportTheme(path);
    }

    private async void OnExportThemeClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { SelectedThemeName: { } name } vm) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title             = "Export Theme",
            SuggestedFileName = name + ".json",
            DefaultExtension  = "json",
            FileTypeChoices   = [ThemeJson],
        });
        if (file?.TryGetLocalPath() is { } path) vm.ExportTheme(path);
    }

    private async void OnDuplicateThemeClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { SelectedThemeName: { } source } vm) return;
        // Existing custom names show as click-to-overwrite, matching the
        // Save Current As… prompt's affordance.
        var name = await NamePromptDialog.Show(
            this, "New theme name:", $"{source} (copy)", "Duplicate Theme", vm.CustomThemeNames);
        if (!string.IsNullOrWhiteSpace(name)) vm.DuplicateTheme(name);
    }

    private void OnDeleteThemeClick(object? sender, RoutedEventArgs e)
        => ViewModel?.DeleteSelectedTheme();
}

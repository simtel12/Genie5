using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Genie.App.Views;

/// <summary>
/// Modal picker shown when the user asks to edit a script that doesn't exist
/// yet AND didn't specify a supported extension (e.g. <c>#edit newscript</c>).
/// Lets them choose which supported script type to create. Returns the chosen
/// extension including the leading dot (".cmd", ".inc", ".js") on Create, or
/// <c>null</c> on Cancel / Esc / close-box.
///
/// <para>The offered types mirror
/// <see cref="ViewModels.MainWindowViewModel.SupportedScriptExtensions"/> —
/// keep the two lists in sync.</para>
/// </summary>
public partial class NewScriptTypeDialog : Window
{
    public NewScriptTypeDialog()
    {
        InitializeComponent();
    }

    /// <summary>Show the picker for <paramref name="scriptName"/> (the bare
    /// name without extension). Returns the chosen extension or null.</summary>
    public static async Task<string?> Show(Window owner, string scriptName)
    {
        var dlg = new NewScriptTypeDialog();
        dlg.PromptText.Text =
            $"Script '{scriptName}' doesn't exist yet.\nChoose the file type to create:";
        return await dlg.ShowDialog<string?>(owner);
    }

    private void OnCreate(object? sender, RoutedEventArgs e)
    {
        var ext = JsOption.IsChecked == true ? ".js"
                : IncOption.IsChecked == true ? ".inc"
                : ".cmd";
        Close(ext);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close((string?)null);
}

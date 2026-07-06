using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Genie.App.Views;

/// <summary>
/// Theme editor dialog (#20). Returns <c>true</c> from ShowDialog when the
/// user saved; <c>false</c> on Cancel or the window's ✕ (both of which mean
/// the caller must restore the pre-edit palette — the VM previews live, so
/// by close time the app is painted with the edited colours either way).
/// </summary>
public partial class ThemeEditorDialog : Window
{
    public ThemeEditorDialog()
    {
        InitializeComponent();
    }

    private void OnSave(object? sender, RoutedEventArgs e)   => Close(true);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}

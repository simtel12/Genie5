using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Genie.App.Views;

/// <summary>
/// A tiny modal, informational popup with a single OK button — for notices the
/// user only needs to acknowledge (e.g. the "Disconnected" leave-game notice,
/// #114). Await <c>ShowDialog(owner)</c>; there is no return value. Closing the
/// window (OK / Enter / Esc / close-box) all dismiss it.
///
/// Designed to be cheap to instantiate — no view-model, no MVVM ceremony,
/// mirroring <see cref="ConfirmDialog"/>.
/// </summary>
public partial class NoticeDialog : Window
{
    public NoticeDialog()
    {
        InitializeComponent();
    }

    public NoticeDialog(string title, string message) : this()
    {
        Title            = title;
        MessageText.Text = message;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}

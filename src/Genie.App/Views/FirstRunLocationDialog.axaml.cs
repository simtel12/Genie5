using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Genie.App.Views;

/// <summary>
/// First-run prompt that asks where Genie should keep its data when neither a
/// local (beside-the-exe) install nor a per-user install exists yet. Backs the
/// portable-first storage model: the default action is <b>Portable</b> (local),
/// and the <see cref="Completion"/> task reports the user's choice.
///
/// <para>Shown ownerless via <see cref="Window.Show()"/> before the main window
/// exists, so it exposes its result through a <see cref="TaskCompletionSource{T}"/>
/// rather than the <c>ShowDialog</c> return value. Closing the window (Esc /
/// close-box) defaults to Portable, keeping the portable-first behavior.</para>
/// </summary>
public partial class FirstRunLocationDialog : Window
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    /// <summary>Completes with <c>true</c> for the local/portable location and
    /// <c>false</c> for the per-user OS folder.</summary>
    public Task<bool> Completion => _tcs.Task;

    public FirstRunLocationDialog()
    {
        InitializeComponent();
    }

    public FirstRunLocationDialog(string localPath, string userPath) : this()
    {
        LocalPathText.Text = localPath;
        UserPathText.Text  = userPath;
    }

    private void OnPortableClick(object? sender, RoutedEventArgs e)
    {
        _tcs.TrySetResult(true);
        Close();
    }

    private void OnUserFolderClick(object? sender, RoutedEventArgs e)
    {
        _tcs.TrySetResult(false);
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        // Esc / close-box without a button press: default to portable-first.
        _tcs.TrySetResult(true);
        base.OnClosed(e);
    }
}

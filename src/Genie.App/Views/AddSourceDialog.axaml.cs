using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Genie.App.ViewModels;
using Genie.Core.Update;

namespace Genie.App.Views;

/// <summary>
/// Modal for adding a new Maps, Plugins, or Scripts update source. Shows the
/// third-party-code acknowledgment for Plugins and Scripts (both executable
/// content) and hides it for Maps (XML data — not executable). The caller
/// awaits <see cref="ShowAsync"/> which returns the parsed
/// <see cref="FeedEntry"/> on success or null on cancel/invalid input.
///
/// Parsing is delegated to <see cref="PluginSourceParser"/>; the dialog
/// just validates and surfaces the parser's error message inline. The
/// caller re-tags the entry's Kind/Extension for its tab (the parser
/// always emits a github-releases plugin shape).
/// </summary>
public partial class AddSourceDialog : Window
{
    private FeedEntry? _result;
    private bool       _requireAcknowledgment;

    public AddSourceDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Show the dialog modally. For Plugins and Scripts the third-party-code
    /// panel appears and the user must check the acknowledgment box before
    /// Add is enabled. Maps mode skips that step.
    /// </summary>
    public static async Task<FeedEntry?> ShowAsync(Window owner, AddSourceKind kind)
    {
        var dlg = new AddSourceDialog();
        dlg._requireAcknowledgment = kind != AddSourceKind.Maps;

        var header  = dlg.FindControl<TextBlock>("HeaderText")!;
        var warn    = dlg.FindControl<Border>("WarningPanel")!;
        var warnTxt = dlg.FindControl<TextBlock>("WarningText")!;
        var ackBox  = dlg.FindControl<CheckBox>("AcknowledgeBox")!;
        var addBtn  = dlg.FindControl<Button>("AddButton")!;

        switch (kind)
        {
            case AddSourceKind.Plugins:
                dlg.Title = "Add Plugin Source";
                header.Text = "Paste a GitHub repo URL or owner/repo shorthand for a plugin you want to install.";
                break;

            case AddSourceKind.Scripts:
                dlg.Title = "Add Scripts Source";
                header.Text = "Paste a GitHub repo URL or owner/repo shorthand for a scripts repository. Update pulls its .cmd / .js / .inc files (subfolders included) into your Scripts folder.";
                warnTxt.Text = "Scripts send commands as your character and can act the moment you run them. Only add sources from authors you trust. Files with the same name in your Scripts folder are overwritten on Update.";
                break;

            default:
                dlg.Title = "Add Maps Source";
                header.Text = "Paste a GitHub repo URL or owner/repo shorthand for a Maps repository.";
                break;
        }

        if (dlg._requireAcknowledgment)
        {
            warn.IsVisible = true;
            addBtn.IsEnabled = false;
            ackBox.IsCheckedChanged += (_, _) => addBtn.IsEnabled = ackBox.IsChecked == true;
        }
        else
        {
            warn.IsVisible = false;
            addBtn.IsEnabled = true;
        }

        await dlg.ShowDialog(owner);
        return dlg._result;
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        _result = null;
        Close();
    }

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        var input  = this.FindControl<TextBox>("UrlInput")!.Text ?? "";
        var error  = this.FindControl<TextBlock>("ErrorText")!;
        var ackBox = this.FindControl<CheckBox>("AcknowledgeBox")!;

        if (_requireAcknowledgment && ackBox.IsChecked != true)
        {
            error.Text      = "Please acknowledge the third-party-code notice to continue.";
            error.IsVisible = true;
            return;
        }

        if (!PluginSourceParser.TryParse(input, out var entry, out var err))
        {
            error.Text      = err ?? "Invalid source URL.";
            error.IsVisible = true;
            return;
        }

        _result = entry;
        Close();
    }
}

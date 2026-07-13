using System;
using Avalonia.Controls;
using Avalonia.Input;
using Genie.App.ViewModels;

namespace Genie.App.Views;

/// <summary>
/// Code-behind for the Script Manager dock tool: the library tree's
/// double-click / Enter → run shortcuts (Genie 4 Script Explorer muscle
/// memory), which pure bindings can't express.
/// </summary>
public partial class ScriptManagerView : UserControl
{
    public ScriptManagerView() => InitializeComponent();

    private ScriptsViewModel? Vm => DataContext as ScriptsViewModel;

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Vm is { SelectedFile.IsFolder: false } vm)
            vm.RunSelectedCommand.Execute().Subscribe();
    }

    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (Vm is { SelectedFile.IsFolder: false } vm)
        {
            vm.RunSelectedCommand.Execute().Subscribe();
            e.Handled = true;
        }
    }

    // Avalonia doesn't move selection on right-click, so without these the
    // context menu would act on whatever was selected BEFORE the click.
    // Both handlers resolve the item under the pointer from the source
    // control's (inherited) DataContext and select it first.
    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;
        if (e.Source is Control { DataContext: ScriptFileNode node } && Vm is { } vm)
            vm.SelectedFile = node;
    }

    private void OnRunningPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;
        if (e.Source is Control { DataContext: RunningScriptRow row } && Vm is { } vm)
            vm.SelectedRunning = row;
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using Genie.App.ViewModels;

namespace Genie.App.Views;

public partial class InventoryViewPanel : UserControl
{
    public InventoryViewPanel()
    {
        InitializeComponent();
    }

    /// <summary>Belt-and-braces selection sync: resolve the node under any
    /// click from the source control's inherited DataContext and push it to
    /// the VM, so the selection-gated buttons (Wiki Lookup / Find in Shops /
    /// Remove) always see the row the user clicked (same pattern as the
    /// Script Manager tree).</summary>
    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Control { DataContext: InventoryNode node } &&
            DataContext is InventoryViewViewModel vm)
            vm.SelectedNode = node;
    }
}

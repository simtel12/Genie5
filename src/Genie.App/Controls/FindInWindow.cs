using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Genie.App.Docking;

namespace Genie.App.Controls;

/// <summary>
/// Visual-side glue for the in-window Find bar (#120). Two attached
/// properties, both taking the tool's <see cref="FindInWindowModel"/>:
///
/// <list type="bullet">
///   <item><see cref="BoxModelProperty"/> — set on the find TextBox: focuses
///         + selects-all whenever the bar opens, and maps Enter → older match,
///         Shift+Enter → newer match, Esc → close.</item>
///   <item><see cref="ScrollModelProperty"/> — set on the window's
///         ScrollViewer: on every jump, brings the matched line's container
///         into view and selects the matched range in its
///         SelectableTextBlock so the hit is visible at a glance. Works
///         because the line hosts are non-virtualised (same assumption
///         AutoScrollBehavior already relies on).</item>
/// </list>
///
/// Set once from a DataTemplate and never unset, so the handlers live for the
/// control's lifetime — the same lifecycle contract as PageScroll.IsTarget.
/// (Auto-follow of new lines can fight a jump while text is streaming in;
/// pairing Find with Pause Scrolling is the intended workflow for that.)
/// </summary>
public static class FindInWindow
{
    public static readonly AttachedProperty<FindInWindowModel?> BoxModelProperty =
        AvaloniaProperty.RegisterAttached<TextBox, FindInWindowModel?>(
            "BoxModel", typeof(FindInWindow));

    public static readonly AttachedProperty<FindInWindowModel?> ScrollModelProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, FindInWindowModel?>(
            "ScrollModel", typeof(FindInWindow));

    public static FindInWindowModel? GetBoxModel(AvaloniaObject o)            => o.GetValue(BoxModelProperty);
    public static void SetBoxModel(AvaloniaObject o, FindInWindowModel? v)    => o.SetValue(BoxModelProperty, v);

    public static FindInWindowModel? GetScrollModel(AvaloniaObject o)         => o.GetValue(ScrollModelProperty);
    public static void SetScrollModel(AvaloniaObject o, FindInWindowModel? v) => o.SetValue(ScrollModelProperty, v);

    static FindInWindow()
    {
        BoxModelProperty.Changed.AddClassHandler<TextBox>((box, e) =>
        {
            if (e.NewValue is not FindInWindowModel model) return;

            // Focus the box when the bar opens. Posted so the bar's
            // IsVisible change has applied before Focus() runs.
            model.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(FindInWindowModel.IsOpen) && model.IsOpen)
                    Dispatcher.UIThread.Post(() =>
                    {
                        box.Focus();
                        box.SelectAll();
                    });
            };

            box.AddHandler(InputElement.KeyDownEvent, (_, ke) =>
            {
                switch (ke.Key)
                {
                    case Key.Escape:
                        model.IsOpen = false;
                        ke.Handled = true;
                        break;
                    case Key.Enter when ke.KeyModifiers.HasFlag(KeyModifiers.Shift):
                        model.NewerCommand.Execute(null);
                        ke.Handled = true;
                        break;
                    case Key.Enter:
                        model.OlderCommand.Execute(null);
                        ke.Handled = true;
                        break;
                }
            }, RoutingStrategies.Tunnel);
        });

        ScrollModelProperty.Changed.AddClassHandler<ScrollViewer>((sv, e) =>
        {
            if (e.NewValue is not FindInWindowModel model) return;
            model.JumpRequested += m => Dispatcher.UIThread.Post(() => Jump(sv, m));
        });
    }

    private static void Jump(ScrollViewer sv, FindInWindowModel.Match match)
    {
        var items = sv.GetVisualDescendants().OfType<ItemsControl>().FirstOrDefault();
        var container = items?.ContainerFromIndex(match.Line);
        if (container is null) return;   // line no longer in the buffer window

        container.BringIntoView();

        // Select the matched range so the hit is visually obvious (renders
        // with the SelectionBrush; Ctrl+C would even copy it).
        var stb = container.GetVisualDescendants().OfType<SelectableTextBlock>().FirstOrDefault();
        if (stb is not null)
        {
            stb.SelectionStart = match.Col;
            stb.SelectionEnd   = match.Col + match.Length;
        }
    }
}

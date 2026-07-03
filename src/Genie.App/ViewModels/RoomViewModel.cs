using System.Reactive.Linq;
using Avalonia.Controls.Documents;
using Genie.App.Highlighting;
using Genie.Core;
using Genie.Core.Events;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Genie.App.ViewModels;

public class RoomViewModel : ReactiveObject
{
    [Reactive] public string Title       { get; private set; } = "";
    [Reactive] public string Description { get; private set; } = "";
    [Reactive] public string Exits       { get; private set; } = "";
    [Reactive] public string Players     { get; private set; } = "";
    [Reactive] public string Objects     { get; private set; } = "";

    /// <summary>
    /// The room-objects line rendered as styled inlines (#131 Room-panel
    /// MonsterBold): DR's &lt;pushBold&gt; creature/NPC names are golded via the
    /// same <see cref="DefaultHighlights.Tokenize"/> path the game streams use,
    /// so it honours the MonsterBold toggle + the `creatures` preset colour. The
    /// panel binds this via InlinesBehavior; plain <see cref="Objects"/> is kept
    /// for the IsVisible gate and copy.
    /// </summary>
    [Reactive] public IReadOnlyList<Inline> ObjectsInlines { get; private set; } = System.Array.Empty<Inline>();

    public void Attach(GenieCore core)
    {
        core.GameEvents.OfType<ComponentEvent>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(e =>
            {
                switch (e.ComponentId)
                {
                    case "room title":   Title       = e.Content; break;
                    case "room desc":    Description = e.Content; break;
                    case "room exits":   Exits       = e.Content; break;
                    case "room players": Players     = e.Content; break;
                    case "room objs":
                        Objects        = e.Content;
                        ObjectsInlines = DefaultHighlights.Tokenize(e.Content, links: null,
                                                                    boldSpans: e.BoldSpans, presetSpans: null);
                        break;
                }
            });
    }
}

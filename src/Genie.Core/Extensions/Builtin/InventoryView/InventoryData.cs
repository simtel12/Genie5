namespace Genie.Core.Extensions.Builtin.InventoryView;

// Ported (in shape) from the Genie 4 InventoryView plugin's iData.cs. The
// on-disk XML element names (ArrayOfCharacterData → CharacterData {name,
// source, items} → ItemData {storage, tap, items}) are what the Genie 4
// plugin's XmlSerializer produced — InventoryViewExtension reads and writes
// that exact shape, so a catalog file moves freely between Genie 4, the old
// V5 DLL plugin, and this built-in.

/// <summary>One character's catalog for a single source (Inventory / Vault /
/// Deed / Home / TraderStorage).</summary>
public sealed class CharacterData
{
    public string Name   { get; set; } = "";
    public string Source { get; set; } = "";
    public List<ItemData> Items { get; } = new();

    public ItemData AddItem(ItemData newItem)
    {
        Items.Add(newItem);
        return newItem;
    }

    public CharacterData Clone()
    {
        var copy = new CharacterData { Name = Name, Source = Source };
        foreach (var item in Items)
            copy.Items.Add(item.Clone(null));
        return copy;
    }
}

/// <summary>A single inventory node (a container or an item). <see cref="Tap"/>
/// is the displayed text; <see cref="Items"/> are its children.</summary>
public sealed class ItemData
{
    public bool   Storage { get; set; }
    public string Tap     { get; set; } = "";

    /// <summary>Back-link to the containing node (null at a source root).
    /// Rebuilt on load; never serialized.</summary>
    public ItemData? Parent { get; set; }

    public List<ItemData> Items { get; } = new();

    public ItemData AddItem(ItemData newItem)
    {
        newItem.Parent = this;
        Items.Add(newItem);
        return newItem;
    }

    public ItemData Clone(ItemData? parent)
    {
        var copy = new ItemData { Storage = Storage, Tap = Tap, Parent = parent };
        foreach (var child in Items)
            copy.Items.Add(child.Clone(copy));
        return copy;
    }
}

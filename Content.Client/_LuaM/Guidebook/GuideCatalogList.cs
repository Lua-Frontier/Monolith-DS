/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Native list and one owned details view, shared by guide catalogues.</summary>
public sealed class GuideCatalogList : BoxContainer
{
    private readonly ItemList _list = new() { HorizontalExpand = true, SetHeight = 240 };
    private readonly BoxContainer _details = new() { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 6 };
    private readonly List<Action<BoxContainer>> _fill = new();

    public GuideCatalogList()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 8;
        AddChild(_list); AddChild(_details);
        _list.OnItemSelected += args => Show(args.ItemIndex);
    }

    public void AddEntry(string name, string summary, Texture? icon, Action<BoxContainer> fill)
    {
        // Keep the text column aligned even when RSI frames have different sizes.
        var scale = icon == null ? 1f : 28f / Math.Max(1, icon.Size.X);
        _list.AddItem(name, icon, iconScale: scale).TooltipText = summary;
        _fill.Add(fill);
    }

    public void SelectFirst()
    {
        if (_fill.Count > 0) _list[0].Selected = true;
    }

    private void Show(int index)
    {
        _details.RemoveAllChildren();
        if (index >= 0 && index < _fill.Count) _fill[index](_details);
    }
}

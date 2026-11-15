/* LuaCorp - AGPLv3. See AGPLv3.txt. */
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Small monochrome pixel icons for guidebook navigation.</summary>
public sealed class GuideNavigationIcon : TextureRect
{
    private static readonly string[] Symbols =
    {
        "newcomer", "manual", "roles", "ship", "sector", "reference", "lore", "discord",
        "rules", "botany", "engineering", "research", "mining", "production", "medical", "combat",
        "work", "expedition", "map", "station", "object", "event", "asteroid", "nebula",
        "clothes", "coins", "crew", "species", "writing", "shiptrade", "fleet", "helm",
        "compass", "cannon", "repair", "flag", "ingots", "chemistry", "recipes", "shield",
    };
    public string Symbol { get; }

    public GuideNavigationIcon(string symbol)
    {
        var index = Array.IndexOf(Symbols, symbol);
        if (index < 0) throw new ArgumentException("Unknown guidebook icon: " + symbol, nameof(symbol));
        Symbol = symbol;
        SetSize = new Vector2(36, 36);
        CanShrink = true;
        Stretch = StretchMode.KeepAspectCentered;
        VerticalAlignment = VAlignment.Top;
        const string path = "/Textures/_LuaM/Interface/Guidebook/Navigation/mono.png";
        var atlas = IoCManager.Resolve<IResourceCache>().GetResource<TextureResource>(path).Texture;
        const int columns = 8;
        var x = index % columns * 32;
        var y = index / columns * 32;
        Texture = new AtlasTexture(atlas, new UIBox2(x + 1, y + 1, x + 31, y + 31));
    }
}

/* LuaM - AGPLv3. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using Content.Client.Guidebook.RichText;
using Content.Client.UserInterface.ControlExtensions;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Native navigation buttons and the sector's Discord invitation.</summary>
public sealed class GuideHome : GridContainer, IDocumentTag
{
    public GuideHome()
    {
        Columns = 3;
        HorizontalExpand = true;
        foreach (var (id, key, icon) in new[]
        {
            ("NewPlayer", "guide-architecture-new-player", "ClothingOuterHardsuitSpatio"),
            ("ColossusGuides", "guide-architecture-colossus-guides", "Wrench"),
            ("Factions", "guide-architecture-factions", "BookTSFSOP"),
            ("NF14", "guide-architecture-n-f14", "ComputerShuttle"),
            ("LuaMWorld", "guide-architecture-lua-m-world", "ComputerAdvancedRadar"),
            ("ColossusCatalogue", "guide-architecture-colossus-catalogue", "RPED"),
            ("LuaMLore", "guide-architecture-lua-m-lore", "MonolithicCrystalLarge"),
            ("", "guide-architecture-discord", "RadioHandheldNF"),
            ("LuaMRuleset", "guide-architecture-lua-m-ruleset", "/Textures/Interface/gavel.png"),
        })
        {
            var button = new Button
            {
                Text = Loc.GetString(key),
                HorizontalExpand = true,
                MinHeight = 76,
                Margin = new Thickness(3),
            };
            button.Label.Orphan();
            button.Label.HorizontalExpand = true;
            var body = new BoxContainer { HorizontalExpand = true, SeparationOverride = 12, Margin = new Thickness(8) };
            var prototypes = IoCManager.Resolve<IPrototypeManager>();
            if (icon.StartsWith('/'))
                body.AddChild(new TextureRect { TexturePath = icon, SetSize = new Vector2(40, 40), CanShrink = true,
                    Stretch = TextureRect.StretchMode.KeepAspectCentered, VerticalAlignment = VAlignment.Center });
            else if (prototypes.TryIndex<EntityPrototype>(icon, out var prototype))
                body.AddChild(new TextureRect { Texture = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>().GetPrototypeIcon(prototype).Default,
                    SetSize = new Vector2(40, 40), CanShrink = true, Stretch = TextureRect.StretchMode.KeepAspectCentered,
                    VerticalAlignment = VAlignment.Center });
            body.AddChild(button.Label); button.AddChild(body);
            button.OnPressed += _ =>
            {
                if (id.Length == 0)
                {
                    IoCManager.Resolve<IUriOpener>().OpenUri("https://discord.gg/sector-colossus");
                }
                else if (button.TryGetParentHandler<ILinkClickHandler>(out var handler))
                    handler.HandleClick(id);
            };
            AddChild(button);
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var columns = availableSize.X < 420 ? 1 : availableSize.X < 640 ? 2 : 3;
        if (Columns != columns) Columns = columns;
        return base.MeasureOverride(availableSize);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = this;
        return true;
    }
}

/* LuaM - AGPLv3. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using Content.Client.Guidebook.RichText;
using Content.Client.UserInterface.ControlExtensions;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

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
            ("NewPlayer", "guide-architecture-new-player", "newcomer"),
            ("ColossusGuides", "guide-architecture-colossus-guides", "manual"),
            ("Factions", "guide-architecture-factions", "roles"),
            ("NF14", "guide-architecture-n-f14", "ship"),
            ("LuaMWorld", "guide-architecture-lua-m-world", "sector"),
            ("ColossusCatalogue", "guide-architecture-colossus-catalogue", "reference"),
            ("LuaMLore", "guide-architecture-lua-m-lore", "lore"),
            ("", "guide-architecture-discord", "discord"),
            ("LuaMRuleset", "guide-architecture-lua-m-ruleset", "rules"),
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
            body.AddChild(new GuideNavigationIcon(icon) { SetSize = new Vector2(40, 40), VerticalAlignment = VAlignment.Center });
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

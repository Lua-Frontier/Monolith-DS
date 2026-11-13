/* LuaM - AGPLv3. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using Content.Client.Guidebook.Richtext;
using Content.Client.Guidebook.RichText;
using Content.Client.UserInterface.ControlExtensions;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Native navigation buttons; Discord uses the connected server's public link.</summary>
public sealed class GuideHome : GridContainer, IDocumentTag
{
    public GuideHome()
    {
        Columns = 3;
        HorizontalExpand = true;
        foreach (var (id, key) in new[]
        {
            ("NewPlayer", "guide-architecture-new-player"),
            ("ColossusGuides", "guide-architecture-colossus-guides"),
            ("Factions", "guide-architecture-factions"),
            ("NF14", "guide-architecture-n-f14"),
            ("LuaMWorld", "guide-architecture-lua-m-world"),
            ("ColossusCatalogue", "guide-architecture-colossus-catalogue"),
            ("LuaMLore", "guide-architecture-lua-m-lore"),
            ("", "guide-architecture-discord"),
            ("LuaMRuleset", "guide-architecture-lua-m-ruleset"),
        })
        {
            var button = new Button
            {
                Text = Loc.GetString(key),
                HorizontalExpand = true,
                MinHeight = 44,
                Margin = new Thickness(3),
            };
            button.OnPressed += _ =>
            {
                if (id.Length == 0)
                {
                    var url = IoCManager.Resolve<IConfigurationManager>().GetCVar(CCVars.InfoLinksDiscord);
                    if (!string.IsNullOrWhiteSpace(url))
                        IoCManager.Resolve<IUriOpener>().OpenUri(url);
                }
                else if (button.TryGetParentHandler<ILinkClickHandler>(out var handler))
                    handler.HandleClick(id);
            };
            AddChild(button);
        }
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = this;
        return true;
    }
}

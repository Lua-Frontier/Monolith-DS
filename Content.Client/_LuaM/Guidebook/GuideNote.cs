/* LuaCorp - AGPLv3. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using Content.Client.Guidebook.Richtext;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Guidebook;

/// <summary>A short, semantic callout; the text remains readable without relying on colour.</summary>
public sealed class GuideNote : PanelContainer, IDocumentTag
{
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Text", out var text)) return false;
        text = text.Replace("&quot;", "\"").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
        var accent = args.GetValueOrDefault("Tone") == "warning" ? Color.FromHex("#E5AD68") : Color.FromHex("#64B9D0");
        HorizontalExpand = true; Margin = new Thickness(0, 4, 0, 14);
        PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#192B36"), BorderColor = accent,
            BorderThickness = new Thickness(3, 0, 0, 0), ContentMarginLeftOverride = 14, ContentMarginRightOverride = 12,
            ContentMarginTopOverride = 10, ContentMarginBottomOverride = 10 };
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromMarkupPermissive(text)); AddChild(label);
        control = this; return true;
    }
}

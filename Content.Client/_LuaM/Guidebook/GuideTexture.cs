/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Guidebook;

/// <summary>
/// An image in a guidebook page, optionally with text to its right: <c>&lt;GuideTexture Texture="/Textures/..." Scale="2" Text="..."/&gt;</c>.
/// </summary>
public sealed class GuideTexture : BoxContainer, IDocumentTag
{
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Texture", out var path))
            return false;

        var scale = args.TryGetValue("Scale", out var scaleText) &&
                    float.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 1f;

        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 10;
        Margin = new Thickness(0, 4);

        AddChild(new TextureRect
        {
            TexturePath = path,
            TextureScale = new Vector2(scale, scale),
            Stretch = TextureRect.StretchMode.KeepCentered,
            VerticalAlignment = VAlignment.Center,
        });

        if (args.TryGetValue("Text", out var text))
        {
            var label = new RichTextLabel
            {
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Center,
            };
            label.SetMessage(FormattedMessage.FromMarkupPermissive(text));
            AddChild(label);
        }
        else
        {
            HorizontalAlignment = HAlignment.Center;
        }

        control = this;
        return true;
    }
}

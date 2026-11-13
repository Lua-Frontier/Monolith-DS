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
    private TextureRect? _image;
    private Vector2 _naturalSize;
    private bool _hasText;

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Texture", out var path))
            return false;

        var scale = 1f;
        if (args.TryGetValue("Scale", out var scaleText) &&
            (!float.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) ||
             !float.IsFinite(scale) || scale <= 0))
            return false;

        _hasText = args.ContainsKey("Text");
        Orientation = _hasText ? LayoutOrientation.Horizontal : LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 10;
        Margin = new Thickness(0, 4);

        _image = new TextureRect
        {
            TexturePath = path,
            CanShrink = true,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };
        _naturalSize = (_image.Texture?.Size ?? Vector2.Zero) * scale;
        if (!float.IsFinite(_naturalSize.X) || !float.IsFinite(_naturalSize.Y) ||
            _naturalSize.X <= 0 || _naturalSize.Y <= 0)
            return false;
        AddChild(_image);

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
        control = this;
        return true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (_image != null)
        {
            var width = float.IsFinite(availableSize.X) ? Math.Max(1, availableSize.X) : _naturalSize.X;
            // Preserve the illustration's proportions and leave most of an icon row for its explanation.
            var imageWidth = _hasText ? width * 0.25f : width;
            _image.SetSize = _naturalSize * Math.Min(1, imageWidth / _naturalSize.X);
        }
        return base.MeasureOverride(availableSize);
    }
}

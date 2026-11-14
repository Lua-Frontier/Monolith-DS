/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Guidebook;

/// <summary>
/// An image in a guidebook page, optionally with text to its right: <c>&lt;GuideTexture Texture="/Textures/..." Scale="2" Text="..."/&gt;</c>.
/// </summary>
public sealed class GuideTexture : BoxContainer, IDocumentTag
{
    private TextureRect? _image;
    private Vector2 _naturalSize;
    private bool _hasText;
    private bool _fill;
    private bool _entity;

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        _entity = args.TryGetValue("Entity", out var entity);
        if (!args.TryGetValue("Texture", out var path) && !_entity)
            return false;

        var scale = 1f;
        if (args.TryGetValue("Scale", out var scaleText) &&
            (!float.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) ||
             !float.IsFinite(scale) || scale <= 0))
            return false;

        _hasText = args.ContainsKey("Text");
        _fill = args.GetValueOrDefault("Fill") == "true";
        Orientation = _hasText ? LayoutOrientation.Horizontal : LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 10;
        Margin = new Thickness(0, 4);

        _image = new TextureRect
        {
            CanShrink = true,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };
        if (_entity)
        {
            var prototypes = IoCManager.Resolve<IPrototypeManager>();
            if (!prototypes.TryIndex<EntityPrototype>(entity!, out var prototype)) return false;
            _image.Texture = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>().GetPrototypeIcon(prototype).Default;
            scale = args.ContainsKey("Scale") ? scale : 2;
        }
        else _image.TexturePath = path!;
        _naturalSize = (_image.Texture?.Size ?? Vector2.Zero) * scale;
        if (!float.IsFinite(_naturalSize.X) || !float.IsFinite(_naturalSize.Y) ||
            _naturalSize.X <= 0 || _naturalSize.Y <= 0)
            return false;
        AddChild(_image);

        if (!_hasText && args.GetValueOrDefault("Caption") is { } caption)
        {
            if (caption == "auto" && _entity)
                caption = IoCManager.Resolve<IPrototypeManager>().Index<EntityPrototype>(entity!).Name;
            var label = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(0, 5, 0, 0) };
            label.SetMessage(FormattedMessage.FromUnformatted(caption));
            AddChild(label);
        }

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
            var factor = _fill && !_entity ? Math.Min(imageWidth / _naturalSize.X, 440 / _naturalSize.Y)
                : Math.Min(1, imageWidth / _naturalSize.X);
            _image.SetSize = _naturalSize * factor;
        }
        return base.MeasureOverride(availableSize);
    }
}

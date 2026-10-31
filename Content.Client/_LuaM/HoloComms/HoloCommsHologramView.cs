using System.Numerics;
using Content.Client.Holopad;
using Content.Client.StatusIcon;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.Holopad;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._LuaM.HoloComms;

/// <summary>
/// Draws the hologram of the other side the same way the holopads show it in the world:
/// the sprite goes into a render target first, then the hologram shader is put over the whole of it.
/// The hologram is a copy of the sprite of the one shown, its clothes bent to the body go without the stencil test,
/// with it the jumpsuits of some species were cut off here and the one shown looked naked.
/// The one shown is framed like a portrait, from the top of the head down to the hips.
/// The sprite is drawn pixel for pixel as in the world and only then made bigger,
/// so a character made shorter or wider keeps square pixels.
/// With nobody to show it draws the holographic phone of the holopads instead.
/// The status icons of the hologram, like the SSD indicator of the one shown, go over it as they do in the world.
/// </summary>
public sealed partial class HoloCommsHologramView : Control
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<ShaderPrototype> HologramShader = "Hologram";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(0.5);

    // Same look as the holopad hologram, used for the phone before there is a hologram to take it from
    private static readonly Color DefaultColor1 = Color.FromHex("#65b8e2");
    private static readonly Color DefaultColor2 = Color.FromHex("#3a6981");
    private const float DefaultAlpha = 0.9f;
    private const float DefaultIntensity = 2f;
    private const float DefaultScrollRate = 0.125f;

    /// <summary>
    /// Until the sprite is measured: the part of its height that is shown, a humanoid has the top of its head and its hips about here.
    /// </summary>
    private const float PortraitTop = 0.12f;
    private const float PortraitBottom = 0.66f;

    /// <summary>
    /// The part of the sprite width a humanoid takes, the rest of the sprite may go past the sides of the screen.
    /// </summary>
    private const float PortraitWidth = 0.6f;

    /// <summary>
    /// How far down from the top of the head the hips are, as a part of the whole height of the one shown.
    /// </summary>
    private const float HipsHeight = 0.58f;

    /// <summary>
    /// Sprite pixels left free above the head.
    /// </summary>
    private const int HeadMargin = 2;

    /// <summary>
    /// Sprite pixels around the sprite in its own target, so nothing at its edges is cut off.
    /// </summary>
    private const int SpritePadding = 2;

    /// <summary>
    /// Fully transparent pixels are not a part of the one shown.
    /// </summary>
    private const byte OpaqueAlpha = 8;

    /// <summary>
    /// The part of the pixels of the one shown that is darker than the ones taken to white, the brightest few can still clip.
    /// </summary>
    private const float BrightestPart = 0.98f;

    /// <summary>
    /// A dark one shown is brightened at most this much, a light one is never made darker than the hologram shows it.
    /// </summary>
    private const float MinIntensity = 1f;
    private const float MaxIntensity = 4f;

    /// <summary>
    /// How much of the real colours of the one shown comes through the hologram.
    /// </summary>
    private const float TrueColors = 0.35f;

    /// <summary>
    /// Status icons are drawn smaller than the portrait, at full scale they would cover the face.
    /// </summary>
    private const float IconScale = 0.4f;

    /// <summary>
    /// The phone takes this part of the screen.
    /// </summary>
    private const float PhoneFill = 0.45f;

    /// <summary>
    /// The holopad phone, used when the hologram has no default sprite of its own.
    /// </summary>
    private static readonly SpriteSpecifier.Rsi DefaultPhone =
        new(new ResPath("/Textures/Structures/Machines/holopad.rsi"), "icon_in_call");

    private readonly SpriteSystem _sprite;
    private readonly HolopadSystem _holopad;
    private readonly StatusIconSystem _statusIcon;
    private readonly SharedTransformSystem _xform;

    // All of them are made on the first draw and freed when the view leaves the screen
    private ShaderInstance? _shader;
    private IRenderTexture? _target;
    private IRenderTexture? _spriteTarget;
    private EntityUid? _hologram;
    private EntityUid? _shownEntity;
    private TimeSpan _nextRefresh;

    /// <summary>
    /// The pixels of the sprite target the one shown takes, read back from it after the sprite is rebuilt.
    /// </summary>
    private UIBox2i? _content;

    /// <summary>
    /// The brightness of the hologram that takes the brightest pixels of the one shown just up to white, read back with <see cref="_content"/>.
    /// </summary>
    private float? _intensity;
    private bool _measure;
    private int _measureVersion;

    /// <summary>
    /// Show the phone when there is nobody on the hologram.
    /// </summary>
    public bool ShowPlaceholder { get; set; }





    public HoloCommsHologramView()
    {
        IoCManager.InjectDependencies(this);

        _sprite = _entManager.System<SpriteSystem>();
        _holopad = _entManager.System<HolopadSystem>();
        _statusIcon = _entManager.System<StatusIconSystem>();
        _xform = _entManager.System<SharedTransformSystem>();

        RectClipContent = true;
    }

    public void SetHologram(EntityUid? hologram)
    {
        if (_hologram == hologram)
            return;

        _hologram = hologram;
        _shownEntity = null;
        _nextRefresh = TimeSpan.Zero;
        ForgetContent();
    }

    protected override void Draw(IRenderHandle handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        if (size.X <= 0 || size.Y <= 0)
            return;

        var hologram = GetHologram();
        if (hologram == null && !ShowPlaceholder)
            return;

        EnsureTarget(size);
        var screen = handle.DrawingHandleScreen;
        float stripes;
        UIBox2? spriteBox = null;
        var scale = 1f;

        if (hologram != null && TryDrawSprite(handle, hologram.Value, size, out var drawnBox, out scale))
        {
            spriteBox = drawnBox;
            stripes = size.Y / scale; // One stripe per pixel of the sprite, like in the world
        }
        else if (ShowPlaceholder)
        {
            stripes = DrawPhone(handle, size);
        }
        else
        {
            return;
        }

        _shader ??= _prototype.Index(HologramShader).InstanceUnique();
        SetShaderParameters(_shader, hologram?.Comp, stripes);

        screen.UseShader(_shader);
        screen.DrawTextureRect(_target!.Texture, UIBox2.FromDimensions(Vector2.Zero, size));
        screen.UseShader(null);

        // The shader leaves only the brightness, light clothes on a light body became one tone and the one shown looked naked
        if (spriteBox != null)
            screen.DrawTextureRect(_target.Texture, UIBox2.FromDimensions(Vector2.Zero, size), Color.White.WithAlpha(TrueColors));

        if (hologram != null && spriteBox != null)
            DrawStatusIcons(screen, hologram.Value, spriteBox.Value, size, scale);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        _target?.Dispose();
        _target = null;
        _spriteTarget?.Dispose();
        _spriteTarget = null;
        _shader?.Dispose();
        _shader = null;
        ForgetContent();
    }

    private Entity<HolopadHologramComponent>? GetHologram()
    {
        if (_hologram is not { } uid ||
            !_entManager.TryGetComponent<HolopadHologramComponent>(uid, out var comp) ||
            comp.LinkedEntity == null)
        {
            return null;
        }

        var now = _timing.CurTime;
        if (_shownEntity != comp.LinkedEntity || now >= _nextRefresh)
        {
            // Somebody else has another height, the old measure does not fit them
            if (_shownEntity != comp.LinkedEntity)
                ForgetContent();

            _shownEntity = comp.LinkedEntity;
            _nextRefresh = now + RefreshInterval;

            // Clothes put on or taken off change the outline
            _measure = true;

            // The hologram is hidden in the world, so nothing rebuilds its sprite unless asked to
            _holopad.RefreshHologram((uid, comp));
        }

        return (uid, comp);
    }

    private bool TryDrawSprite(IRenderHandle handle, Entity<HolopadHologramComponent> hologram, Vector2i size, out UIBox2 spriteBox, out float scale)
    {
        spriteBox = default;
        scale = 1f;

        if (!_entManager.TryGetComponent<SpriteComponent>(hologram, out var sprite) ||
            !_entManager.TryGetComponent<TransformComponent>(hologram, out var xform))
        {
            return false;
        }

        if (!TryDrawShown(handle, (hologram, sprite, xform), out var spriteSize))
            return false;

        if (_measure)
        {
            _measure = false;
            MeasureContent();
        }

        // Until it is measured a humanoid is assumed to stand in the middle of its sprite
        var content = _content ?? UIBox2i.FromDimensions(
            (int) (spriteSize.X * (1f - PortraitWidth) / 2f),
            (int) (spriteSize.Y * PortraitTop),
            (int) (spriteSize.X * PortraitWidth),
            (int) (spriteSize.Y * (PortraitBottom - PortraitTop) / HipsHeight));

        // Shown from just above the head down to the hips, the head stays at the top of the screen
        var shownTop = content.Top - HeadMargin;
        var shownHeight = HeadMargin + content.Height * HipsHeight;
        var shownWidth = MathF.Min(content.Width, shownHeight * 1.5f);

        // The largest whole scale that fits keeps the pixels square
        var fit = MathF.Min(size.Y / shownHeight, size.X / shownWidth);
        scale = MathF.Max(1f, MathF.Floor(fit));

        var drawnSize = (Vector2) spriteSize * scale;
        var origin = new Vector2(MathF.Round((size.X - drawnSize.X) / 2f), -shownTop * scale);
        var drawnBox = UIBox2.FromDimensions(origin, drawnSize);

        handle.DrawingHandleScreen.RenderInRenderTarget(_target!, () =>
        {
            ResetTargetState(handle);
            handle.DrawingHandleScreen.DrawTextureRect(_spriteTarget!.Texture, drawnBox);
        }, Color.Transparent);

        spriteBox = new UIBox2(
            origin + (Vector2) content.TopLeft * scale,
            origin + (Vector2) content.BottomRight * scale);

        return true;
    }

    /// <summary>
    /// The sprite pixel for pixel, without its scale: the height and width of the character squeezed whole rows of pixels out
    /// and a small one came out flattened, the screen fits the portrait to itself anyway.
    /// The typing bubble is a layer of the sprite too, it is left out: with it the portrait jumped every time they typed.
    /// </summary>
    private bool TryDrawShown(IRenderHandle handle, Entity<SpriteComponent, TransformComponent> shown, out Vector2i spriteSize)
    {
        spriteSize = default;

        var bubble = shown.Comp1.LayerMapTryGet(TypingIndicatorLayers.Base, out var bubbleLayer) &&
                     shown.Comp1[bubbleLayer].Visible;

        if (bubble)
            _sprite.LayerSetVisible((shown, shown.Comp1), bubbleLayer, false);

        var scale = shown.Comp1.Scale;
        var scaled = scale != Vector2.One;
        if (scaled)
            _sprite.SetScale((shown, shown.Comp1), Vector2.One);

        try
        {
            var bounds = _sprite.GetLocalBounds((shown, shown.Comp1));
            if (bounds.Width <= 0f || bounds.Height <= 0f)
                return false;

            var pixelsPerMeter = EyeManager.PixelsPerMeter;
            spriteSize = new Vector2i(
                (int) MathF.Ceiling(bounds.Width * pixelsPerMeter) + SpritePadding * 2,
                (int) MathF.Ceiling(bounds.Height * pixelsPerMeter) + SpritePadding * 2);
            EnsureSpriteTarget(spriteSize);

            var center = bounds.Center + shown.Comp1.Offset;
            var position = Vector2.Round((Vector2) spriteSize / 2f - new Vector2(center.X, -center.Y) * pixelsPerMeter);

            _sprite.ForceUpdate(shown);

            handle.DrawingHandleScreen.RenderInRenderTarget(_spriteTarget!, () =>
            {
                ResetTargetState(handle);
                handle.DrawEntity(shown, position, Vector2.One, Angle.Zero, Angle.Zero, Direction.South, shown.Comp1, shown.Comp2, _xform);
            }, Color.Transparent);

            return true;
        }
        finally
        {
            if (scaled)
                _sprite.SetScale((shown, shown.Comp1), scale);

            if (bubble)
                _sprite.LayerSetVisible((shown, shown.Comp1), bubbleLayer, true);
        }
    }

    /// <summary>
    /// Reads the sprite target back and finds the pixels the one shown takes, the top of the head, the feet and the sides, and how bright they are.
    /// The pixels come a few frames later, the view keeps the last measure until then.
    /// </summary>
    private void MeasureContent()
    {
        var version = _measureVersion;
        _spriteTarget!.CopyPixelsToMemory<Rgba32>(image =>
        {
            if (version == _measureVersion)
            {
                _content = FindContent(image);
                _intensity = FindIntensity(image);
            }
        });
    }

    private static UIBox2i? FindContent(SixLabors.ImageSharp.Image<Rgba32> image)
    {
        var left = int.MaxValue;
        var top = int.MaxValue;
        var right = -1;
        var bottom = -1;

        // The sandbox has no indexer on the image, its pixels are read in rows
        var pixels = image.GetPixelSpan();
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (pixels[y * image.Width + x].A < OpaqueAlpha)
                    continue;

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < 0 ? null : new UIBox2i(left, top, right + 1, bottom + 1);
    }

    /// <summary>
    /// The holopads double the brightness, light clothes on a light body both went white and the one shown looked naked.
    /// Here the brightest pixels of the one shown, a few aside, end up just white and everything darker keeps its difference.
    /// </summary>
    private static float? FindIntensity(SixLabors.ImageSharp.Image<Rgba32> image)
    {
        var brightness = new List<float>();
        foreach (var pixel in image.GetPixelSpan())
        {
            if (pixel.A < OpaqueAlpha)
                continue;

            // The shader works on linear colours, the same as it gets them from the target
            brightness.Add(0.2126f * ToLinear(pixel.R) + 0.7152f * ToLinear(pixel.G) + 0.0722f * ToLinear(pixel.B));
        }

        if (brightness.Count == 0)
            return null;

        brightness.Sort();
        var brightest = brightness[(int) ((brightness.Count - 1) * BrightestPart)];
        return Math.Clamp(1f / MathF.Max(brightest, 0.01f), MinIntensity, MaxIntensity);
    }

    private static float ToLinear(byte value)
    {
        return MathF.Pow(value / 255f, 2.2f);
    }

    private void ForgetContent()
    {
        _content = null;
        _intensity = null;
        _measureVersion++;
    }

    private void EnsureSpriteTarget(Vector2i size)
    {
        if (_spriteTarget != null && _spriteTarget.Size == size)
            return;

        _spriteTarget?.Dispose();
        _spriteTarget = _clyde.CreateRenderTarget(size,
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb, true),
            name: nameof(HoloCommsHologramView));

        // The measure was of the old size
        ForgetContent();
        _measure = true;
    }

    /// <summary>
    /// Same places as in the world: the top corners, the base icons stacked down from there.
    /// The corners are those of the one shown, as far as they are on the screen.
    /// </summary>
    private void DrawStatusIcons(DrawingHandleScreen screen, EntityUid hologram, UIBox2 spriteBox, Vector2i size, float scale)
    {
        var icons = _statusIcon.GetStatusIcons(hologram);
        if (icons.Count == 0)
            return;

        icons.Sort();
        var left = 0f;
        var right = 0f;
        scale = MathF.Max(1f, MathF.Floor(scale * IconScale));

        // The icons go at the sides of the one shown, kept on the screen
        var boxLeft = MathF.Max(spriteBox.Left, 0f);
        var boxTop = MathF.Max(spriteBox.Top, 0f) + 4f;
        spriteBox = new UIBox2(
            boxLeft,
            boxTop,
            MathF.Max(boxLeft, MathF.Min(spriteBox.Right, size.X)),
            MathF.Max(boxTop, spriteBox.Bottom));

        foreach (var icon in icons)
        {
            var texture = _sprite.GetFrame(icon.Icon, _timing.RealTime);
            var iconSize = (Vector2) texture.Size * scale;
            var onRight = icon.LocationPreference == StatusIconLocationPreference.Right;
            var position = onRight
                ? new Vector2(spriteBox.Right - iconSize.X, spriteBox.Top + right)
                : new Vector2(spriteBox.Left, spriteBox.Top + left);

            screen.DrawTextureRect(texture, UIBox2.FromDimensions(position, iconSize));

            if (icon.Layer != StatusIconLayer.Base)
                continue;

            if (onRight)
                right += iconSize.Y;
            else
                left += iconSize.Y;
        }
    }

    /// <summary>
    /// The phone the holopads show while nobody stands at the other end.
    /// </summary>
    private float DrawPhone(IRenderHandle handle, Vector2i size)
    {
        var specifier = DefaultPhone;
        if (_hologram is { } uid &&
            _entManager.TryGetComponent<HolopadHologramComponent>(uid, out var comp) &&
            !string.IsNullOrEmpty(comp.RsiPath) && !string.IsNullOrEmpty(comp.RsiState))
        {
            specifier = new SpriteSpecifier.Rsi(new ResPath("/Textures") / comp.RsiPath, comp.RsiState);
        }

        var texture = _sprite.Frame0(specifier);
        var fit = MathF.Min(size.X, size.Y) * PhoneFill / MathF.Max(texture.Width, texture.Height);
        var scale = MathF.Max(1f, MathF.Floor(fit));
        var drawnSize = (Vector2) texture.Size * scale;
        var box = UIBox2.FromDimensions(((Vector2) size - drawnSize) / 2f, drawnSize);

        // Slowly pulses while waiting
        var pulse = 0.75f + 0.25f * MathF.Sin((float) _timing.RealTime.TotalSeconds * 3f);

        var screen = handle.DrawingHandleScreen;
        screen.RenderInRenderTarget(_target!, () =>
        {
            ResetTargetState(handle);
            screen.DrawTextureRect(texture, box, Color.White.WithAlpha(pulse));
        }, Color.Transparent);

        return size.Y / scale; // One stripe per pixel of the phone, like in the world
    }

    /// <summary>
    /// The UI draws with the window offset and clip, inside the target everything starts from its corner.
    /// The previous state comes back once the target is done.
    /// </summary>
    private static void ResetTargetState(IRenderHandle handle)
    {
        handle.SetScissor(null);
        handle.DrawingHandleScreen.SetTransform(Matrix3x2.Identity);
    }

    private void SetShaderParameters(ShaderInstance shader, HolopadHologramComponent? hologram, float stripes)
    {
        var color1 = hologram?.Color1 ?? DefaultColor1;
        var color2 = hologram?.Color2 ?? DefaultColor2;
        var scrollRate = hologram?.ScrollRate ?? DefaultScrollRate;

        shader.SetParameter("color1", new Vector3(color1.R, color1.G, color1.B));
        shader.SetParameter("color2", new Vector3(color2.R, color2.G, color2.B));
        shader.SetParameter("alpha", hologram?.Alpha ?? DefaultAlpha);
        shader.SetParameter("intensity", _intensity ?? hologram?.Intensity ?? DefaultIntensity);
        shader.SetParameter("texHeight", stripes);
        shader.SetParameter("t", (float) _timing.CurTime.TotalSeconds * scrollRate);
    }

    private void EnsureTarget(Vector2i size)
    {
        if (_target != null && _target.Size == size)
            return;

        _target?.Dispose();
        _target = _clyde.CreateRenderTarget(size,
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb, true),
            name: nameof(HoloCommsHologramView));
    }
}

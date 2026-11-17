// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Numerics;
using System.Linq;
using Content.Shared._LuaM.Announcements;
using Content.Client._LuaM.Hud;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._LuaM.Announcements;

/// <summary>A bounded, uniformly scaled transmission. Message layout is independent of reveal progress.</summary>
public sealed partial class ScreenAnnouncementCard : Control
{
    [Dependency] private IResourceCache _resources = default!;
    private readonly FontResource _font;
    private readonly AnnouncementTextLabel _title;
    private readonly AnnouncementTextLabel _body;
    private readonly AnnouncementTextLabel _name;
    private readonly ScrollContainer _scroll;
    private readonly Control _projection;
    private readonly TextureRect _projectionArt;
    private readonly TextureRect _divider;
    private readonly AnnouncementPortraitView _portrait;
    private readonly TextureRect _insignia;
    private StyleBoxTexture? _frame;
    private string _speakerName = string.Empty;
    private float _effectiveScale = -1;
    private float _presentationScale = 1;
    private Color _accent;
    public const int MaximumBodyLength = 8192;
    public const float MaximumBodyWidth = 640;
    private const float LeftInset = 28;
    private const float TopInset = 22;
    private const float RightInset = 32;
    private const float BottomInset = 30;
    private const float HeaderRightInset = 128;
    private const float TextLeft = LeftInset + 133 + 18;
    private const float MinimumFrameWidth = 330;
    private float _contentTop;
    private float _nameHeight;
    private float _bodyHeight;
    private Vector2 _measuredFrame;
    private float _viewportWidth;
    public Color MessageColor => _body.TextColor;
    public bool PreviewMode { get; set; }
    public float PresentationScale
    {
        get => _presentationScale;
        set
        {
            var scale = HudLayout.NormalizeScale(value, 1);
            if (_presentationScale == scale) return;
            _presentationScale = scale;
            InvalidateMeasure();
        }
    }

    public ScreenAnnouncementCard()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
        _font = _resources.GetResource<FontResource>("/Fonts/_LuaM/PTMono/PTM55FT.ttf");
        _title = new AnnouncementTextLabel { Name = "AnnouncementTitle", MouseFilter = MouseFilterMode.Ignore, RectClipContent = true, WrapText = false };
        _body = new AnnouncementTextLabel { Name = "AnnouncementBody", MouseFilter = MouseFilterMode.Ignore, LineHeightScale = 1.12f };
        _name = new AnnouncementTextLabel { Name = "AnnouncementSpeakerName", MouseFilter = MouseFilterMode.Ignore, RectClipContent = true, MaximumLines = 3, FadeLastLine = true };
        _projection = new Control { Name = "AnnouncementProjection", RectClipContent = true, MouseFilter = MouseFilterMode.Ignore };
        _projectionArt = new TextureRect { MouseFilter = MouseFilterMode.Ignore, CanShrink = true, Stretch = TextureRect.StretchMode.Scale };
        _divider = new TextureRect { Name = "AnnouncementDivider", MouseFilter = MouseFilterMode.Ignore, CanShrink = true, Stretch = TextureRect.StretchMode.Scale };
        _insignia = new TextureRect { Name = "AnnouncementInsignia", MouseFilter = MouseFilterMode.Ignore, CanShrink = true, Stretch = TextureRect.StretchMode.Scale };
        _portrait = new AnnouncementPortraitView { Name = "AnnouncementPortrait", Visible = false,
            HorizontalAlignment = HAlignment.Center, VerticalAlignment = VAlignment.Bottom };
        _projection.AddChild(_projectionArt);
        _projection.AddChild(_insignia);
        _projection.AddChild(_portrait);
        _scroll = new ScrollContainer { Name = "AnnouncementMessageScroll", HScrollEnabled = false,
            ReserveScrollbarSpace = true, MouseFilter = MouseFilterMode.Pass };
        _scroll.AddChild(_body);
        AddChild(_projection);
        AddChild(_divider);
        AddChild(_title);
        AddChild(_scroll);
        AddChild(_name);

    }

    public void SetAnnouncement(ScreenAnnouncementEvent announcement)
    {
        // Alert-code/chat colours must never change the selected banner's typography or hologram.
        _accent = GetAccent(announcement.Style);
        var artwork = announcement.Style switch
        {
            ScreenAnnouncementStyle.Tsf => "tsf", ScreenAnnouncementStyle.Vanguard => "vanguard",
            ScreenAnnouncementStyle.Mmc => "mmc", ScreenAnnouncementStyle.Ussp => "ussp",
            ScreenAnnouncementStyle.Viper => "viper", ScreenAnnouncementStyle.Medical => "medical",
            ScreenAnnouncementStyle.Colossus => "colossus", ScreenAnnouncementStyle.SystemRed => "system-red",
            ScreenAnnouncementStyle.SystemPurple => "system-purple", _ => "system",
        };
        Texture Load(string layer) => _resources.GetResource<TextureResource>(
            $"/Textures/_LuaM/Interface/Announcements/{artwork}-{layer}.png").Texture;
        _frame = new StyleBoxTexture { Texture = Load("chrome"), PatchMarginLeft = 180, PatchMarginRight = 125,
            PatchMarginTop = 80, PatchMarginBottom = 24 };
        _projectionArt.Texture = Load("projection");
        _divider.Texture = Load("divider");
        _insignia.Texture = Load("emblem");
        _insignia.Visible = announcement.Portrait == null;
        _portrait.Visible = false;
        _portrait.Accent = _accent;
        _portrait.SetSnapshot(announcement.Portrait);
        _portrait.Visible = announcement.Portrait != null;
        _speakerName = string.Join(" ", (announcement.Portrait?.Name ?? string.Empty)
            .Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries));
        _name.Visible = announcement.Portrait != null && _speakerName.Length > 0;
        _name.SetText(ClipText(_speakerName, 256), Color.InterpolateBetween(Color.White, _accent, 0.22f));
        var heading = string.Join(" ", ClipText(announcement.Title, 256).Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries));
        _title.SetText(heading, Color.InterpolateBetween(Color.White, _accent, 0.22f));
        _title.Visible = announcement.Title.Length > 0;
        _body.SetText(ClipText(announcement.Message, MaximumBodyLength), Color.InterpolateBetween(Color.FromHex("#E2EBF1"), _accent, 0.3f));
        _effectiveScale = -1;
        _scroll.SetScrollValue(Vector2.Zero);
        SetProgress(0);
        InvalidateMeasure();
    }

    private static string ClipText(string text, int limit)
    {
        if (text.Length <= limit) return text;
        if (char.IsLowSurrogate(text[limit])) limit--;
        return text[..limit];
    }

    private int MetadataElements => Math.Max(_title.ElementCount, _name.ElementCount);
    private float HeadingRevealDuration => MetadataElements == 0 ? 0 : Math.Clamp(MetadataElements / 45f, 0.2f, 1.2f);
    private float BodyRevealDuration => _body.ElementCount == 0 ? 0 : 1.2f / 0.7f;
    public float RevealDuration => HeadingRevealDuration + BodyRevealDuration;

    /// <summary>Metadata types concurrently at its own cadence; only then does the body start.</summary>
    public void SetElapsed(float elapsed)
    {
        if (!float.IsFinite(elapsed)) elapsed = 0;
        elapsed = Math.Max(0, elapsed);
        var metadata = HeadingRevealDuration > 0 ? Math.Clamp(elapsed / HeadingRevealDuration, 0, 1) : 1;
        _title.VisibleElements = (int) MathF.Ceiling(metadata * _title.ElementCount);
        _name.VisibleElements = (int) MathF.Ceiling(metadata * _name.ElementCount);
        var body = BodyRevealDuration > 0 ? Math.Clamp((elapsed - HeadingRevealDuration) / BodyRevealDuration, 0, 1) : 1;
        _body.VisibleElements = (int) MathF.Ceiling(body * _body.ElementCount);
    }

    public void SetProgress(float progress)
    {
        if (!float.IsFinite(progress)) progress = 0;
        var count = (int) MathF.Ceiling(Math.Clamp(progress, 0, 1) * (MetadataElements + _body.ElementCount));
        _title.VisibleElements = Math.Min(count, _title.ElementCount);
        _body.VisibleElements = Math.Clamp(count - MetadataElements, 0, _body.ElementCount);
        _name.VisibleElements = Math.Min(count, _name.ElementCount);
    }

    public static Color GetAccent(ScreenAnnouncementStyle style) => Color.FromHex(style switch
    {
        ScreenAnnouncementStyle.SystemRed => "#EF5B5B", ScreenAnnouncementStyle.SystemPurple => "#BB87ED",
        ScreenAnnouncementStyle.Tsf => "#5ABEFF", ScreenAnnouncementStyle.Vanguard => "#F26868",
        ScreenAnnouncementStyle.Mmc => "#FCA503", ScreenAnnouncementStyle.Ussp => "#DF6788",
        ScreenAnnouncementStyle.Viper => "#EF5B5B", ScreenAnnouncementStyle.Medical => "#7EAFFF",
        ScreenAnnouncementStyle.Colossus => "#64C692", ScreenAnnouncementStyle.Neutral => "#A7C6D7",
        _ => "#F4BC53",
    });


    private void SetEffectiveScale(float scale)
    {
        if (Math.Abs(_effectiveScale - scale) < 0.0001f) return;
        _effectiveScale = scale;
        _title.Typeface = new VectorFont(_font, Math.Max(1, (int) MathF.Round(20 * scale)));
        _body.Typeface = new VectorFont(_font, Math.Max(1, (int) MathF.Round(14 * scale)));
        _projection.SetSize = new Vector2(133, 103) * scale;
        _portrait.SetSize = new Vector2(100, 77) * scale;
        _portrait.Margin = new Thickness(0, 0, 0, Math.Max(scale, 1 / UIScale));
        if (_frame != null) _frame.TextureScale = new Vector2(scale);
        _scroll.ScrollSpeedY = Math.Max(12, (int) (42 * scale));
        _scroll.MouseFilter = PreviewMode ? MouseFilterMode.Ignore : MouseFilterMode.Pass;
        foreach (var bar in _scroll.Children.OfType<ScrollBar>())
        {
            bar.MouseFilter = PreviewMode ? MouseFilterMode.Ignore : MouseFilterMode.Stop;
            bar.MinWidth = 5 * scale;
        }
        _scroll.Stylesheet = new Stylesheet(new[] { new StyleRule(
            new SelectorElement(typeof(VScrollBar), null, null, null), new[] {
                new StyleProperty(ScrollBar.StylePropertyTrack, new StyleBoxFlat(_accent.WithAlpha(0.12f))),
                new StyleProperty(ScrollBar.StylePropertyGrabber, new StyleBoxFlat(_accent.WithAlpha(0.65f)) {
                    ContentMarginLeftOverride = 1, ContentMarginRightOverride = 1,
                    ContentMarginTopOverride = 6, ContentMarginBottomOverride = 6 }),
            }) });
        _name.Typeface = new VectorFont(_font, Math.Max(1, (int) MathF.Round(11 * scale)));
    }

    protected override Vector2 MeasureCore(Vector2 availableSize)
    {
        _viewportWidth = availableSize.X;
        // Native MeasureCore clamps desired width to the offered space. Only the single-row
        // heading may exceed that space; body wrapping still uses the real viewport width.
        return base.MeasureCore(new Vector2(float.PositiveInfinity, availableSize.Y));
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        // Content does not shrink simply because the message is short. Only the user's scale
        // and an exceptionally short window change fonts/portrait size together.
        var scale = PresentationScale;
        for (var pass = 0; pass < 3; pass++)
        {
            SetEffectiveScale(scale);
            _title.Measure(new Vector2(float.PositiveInfinity));
            _name.Measure(new Vector2(133 * scale, float.PositiveInfinity));
            _nameHeight = _name.Visible ? _name.DesiredSize.Y : 0;
            _contentTop = TopInset * scale + (_title.Visible ? _title.DesiredSize.Y + 12 * scale : 0);
            _bodyHeight = 103 * scale + (_nameHeight > 0 ? 5 * scale + _nameHeight : 0);
            var height = _contentTop + _bodyHeight + BottomInset * scale;
            if (height <= availableSize.Y || pass == 2) break;
            scale = Math.Max(0.001f, scale * availableSize.Y / height);
        }
        var right = RightInset * scale;
        var textLeft = TextLeft * scale;
        var normalWidth = Math.Max(MinimumFrameWidth * scale, Math.Min(MaximumBodyWidth * scale, _viewportWidth));
        var bodyWidth = Math.Max(1, normalWidth - textLeft - right - 5 * scale);
        _body.Measure(new Vector2(bodyWidth, float.PositiveInfinity));
        // Keep space for corner ornaments even on short messages. The heading reserves the
        // entire decorated right patch; expanding the panel never stretches its glyphs.
        var width = Math.Max(MinimumFrameWidth * scale, Math.Max((LeftInset + HeaderRightInset) * scale + _title.DesiredSize.X + 1 / UIScale,
            textLeft + right + _body.DesiredSize.X + 6 * scale));
        width = MathF.Ceiling(width * UIScale) / UIScale;
        _projection.Measure(new Vector2(133, 103) * scale);
        _scroll.Measure(new Vector2(Math.Max(1, width - textLeft - right), _bodyHeight));
        _measuredFrame = new Vector2(width, _contentTop + _bodyHeight + BottomInset * scale);
        return _measuredFrame;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var scale = _effectiveScale > 0 ? _effectiveScale : PresentationScale;
        void Place(Control child, Vector2 position, Vector2 size) =>
            child.Arrange(UIBox2.FromDimensions(position, Vector2.Max(Vector2.Zero, size)));
        Place(_projection, new Vector2(LeftInset * scale, _contentTop), new Vector2(133, 103) * scale);
        Place(_title, new Vector2(LeftInset, TopInset) * scale, new Vector2(Math.Max(1, finalSize.X - (LeftInset + HeaderRightInset) * scale), _title.DesiredSize.Y));
        _divider.Visible = _title.Visible;
        Place(_divider, new Vector2(LeftInset * scale, _contentTop - 7 * scale), new Vector2(Math.Max(1, finalSize.X - (LeftInset + RightInset) * scale), 3 * scale));
        Place(_name, new Vector2(LeftInset * scale, _contentTop + 108 * scale), new Vector2(133 * scale, _nameHeight));
        Place(_scroll, new Vector2(TextLeft * scale, _contentTop), new Vector2(Math.Max(1, finalSize.X - (TextLeft + RightInset) * scale),
            Math.Max(0, Math.Min(_bodyHeight, finalSize.Y - _contentTop - BottomInset * scale))));
        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (PixelWidth <= 0 || PixelHeight <= 0 || _frame?.Texture == null) return;
        _body.MinimumVisibleHeight = _scroll.VScroll;
        _body.MaximumVisibleHeight = _scroll.VScroll + _scroll.Height;
        var scale = UIScale * _frame.TextureScale.X;
        // Guard transient geometry: asymmetric patch clamping can otherwise invert UIBox2.
        if (PixelWidth < (_frame.PatchMarginLeft + _frame.PatchMarginRight) * scale ||
            PixelHeight < (_frame.PatchMarginTop + _frame.PatchMarginBottom) * scale)
            handle.DrawTextureRect(_frame.Texture, PixelSizeBox);
        else
            _frame.Draw(handle, PixelSizeBox, UIScale);
    }
}

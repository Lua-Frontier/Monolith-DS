/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 * Ported from Wolfgate (_WF/FtlEffects), AGPLv3.
 */

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Shuttles.FtlEffects;

// -- Сохраняет космос под улетающим корпусом до отрисовки мира
public sealed partial class FtlMotionBackgroundOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CopyShader = "LuaMFtlCopy";
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    private readonly FtlDepartureSystem _system;
    private readonly ShaderInstance _copy;
    private IRenderTexture? _background;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;
    public override bool RequestScreenTexture => true;

    public Texture? Background => _background?.Texture;

    public long ViewportId { get; private set; } = -1;

    public FtlMotionBackgroundOverlay(FtlDepartureSystem system)
    {
        IoCManager.InjectDependencies(this);
        _system = system;
        _copy = _prototypes.Index(CopyShader).Instance();
        ZIndex = int.MaxValue;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        ViewportId = -1;
        return args.Viewport.Eye != null && _system.HasMotion(args.MapId, args.WorldAABB);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;
        var size = args.Viewport.Size;
        if (_background == null || _background.Size != size)
        {
            _background?.Dispose();
            _background = _clyde.CreateRenderTarget(size, RenderTargetColorFormat.Rgba8Srgb,
                new TextureSampleParameters { Filter = true }, nameof(FtlMotionBackgroundOverlay));
        }
        var handle = args.RenderHandle.DrawingHandleScreen;
        var target = _background;
        var texture = ScreenTexture;
        handle.RenderInRenderTarget(target, () =>
        {
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(_copy);
            handle.DrawTextureRect(texture, UIBox2.FromDimensions(Vector2.Zero, size));
        }, Color.Transparent);
        ViewportId = args.Viewport.Id;
    }

    protected override void DisposeBehavior()
    {
        _background?.Dispose();
        base.DisposeBehavior();
    }
}

using System.Numerics;
using Content.Shared._LuaM.CCVar;
using Content.Shared._LuaM.Lobby;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Lobby;

public sealed class LobbyBackgroundControl : TextureRect
{
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IConfigurationManager _configuration = default!;

    private const int FramesLoadedPerUpdate = 1;
    private const float ParallaxZoom = 1.06f;
    private const float ParallaxTravel = 0.0125f;
    private const float ParallaxResponse = 6f;

    private readonly List<Texture> _frames = new();
    private readonly List<float> _delays = new();
    private readonly Queue<(ResPath Path, float Delay)> _pending = new();
    private int _frame;
    private float _timer;
    private Vector2 _cursorOffset;

    public LobbyBackgroundControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    public void SetStatic(Texture? texture)
    {
        Clear();
        Texture = texture;
    }

    public void SetAnimated(AnimatedLobbyBackgroundPrototype proto)
    {
        Clear();

        for (var i = 0; i < proto.Frames; i++)
        {
            var delay = MathF.Max(0.02f, i < proto.Delays.Count ? proto.Delays[i] : proto.FrameTime);
            _pending.Enqueue((proto.Path / $"{i:D3}.{proto.Extension}", delay));
        }

        LoadPending(1);
        Texture = _frames.Count > 0 ? _frames[0] : null;
    }

    private void Clear()
    {
        _frames.Clear();
        _delays.Clear();
        _pending.Clear();
        _frame = 0;
        _timer = 0;
    }

    private void LoadPending(int count)
    {
        for (var i = 0; i < count && _pending.TryDequeue(out var next); i++)
        {
            if (!_resourceCache.TryGetResource<TextureResource>(next.Path, out var texture))
                continue;

            _frames.Add(texture.Texture);
            _delays.Add(next.Delay);
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateParallax(args.DeltaSeconds);

        if (_pending.Count > 0)
        {
            LoadPending(FramesLoadedPerUpdate);
            return;
        }

        if (_frames.Count < 2 || !VisibleInTree)
            return;

        _timer += args.DeltaSeconds;
        var changed = false;
        while (_timer >= _delays[_frame])
        {
            _timer -= _delays[_frame];
            _frame = (_frame + 1) % _frames.Count;
            changed = true;
        }

        if (changed)
            Texture = _frames[_frame];
    }

    private void UpdateParallax(float frameTime)
    {
        if (Texture == null || Size.X <= 0 || Size.Y <= 0 || !ParallaxEnabled)
        {
            _cursorOffset = Vector2.Zero;
            return;
        }

        var cursor = UserInterfaceManager.MousePositionScaled.Position - GlobalPosition;
        var target = Vector2.Clamp(cursor / Size * 2f - Vector2.One, -Vector2.One, Vector2.One);
        _cursorOffset = Vector2.Lerp(_cursorOffset, target, 1f - MathF.Exp(-ParallaxResponse * frameTime));
    }

    private bool ParallaxEnabled => _configuration.GetCVar(LuaMCVars.LobbyBackgroundParallax)
                                    && !_configuration.GetCVar(CCVars.ReducedMotion);

    protected override void Draw(DrawingHandleScreen handle)
    {
        var texture = Texture;
        if (texture == null || PixelSize.X <= 0 || PixelSize.Y <= 0)
            return;

        var viewport = (Vector2) PixelSize;
        var textureSize = (Vector2) texture.Size;
        var scale = MathF.Max(viewport.X / textureSize.X, viewport.Y / textureSize.Y) * (ParallaxEnabled ? ParallaxZoom : 1f);
        var origin = (viewport - textureSize * scale) / 2f + _cursorOffset * viewport * ParallaxTravel;

        handle.DrawTextureRectRegion(texture, PixelSizeBox, new UIBox2(-origin / scale, (viewport - origin) / scale));
    }
}

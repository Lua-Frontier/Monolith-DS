using Content.Shared._LuaM.Lobby;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Lobby;

public sealed class LobbyBackgroundControl : TextureRect
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const int FramesLoadedPerUpdate = 1;

    private readonly List<Texture> _frames = new();
    private readonly List<float> _delays = new();
    private readonly Queue<(ResPath Path, float Delay)> _pending = new();
    private int _frame;
    private float _timer;

    public LobbyBackgroundControl()
    {
        IoCManager.InjectDependencies(this);
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
}

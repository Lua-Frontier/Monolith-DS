using Content.Shared._LuaM.Cyberware;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._LuaM.Cyberware;

/// <summary>
/// Adds the cyberpsychosis glitch overlay for the local player while they have <see cref="CyberpsychosisGlitchComponent"/>.
/// </summary>
public sealed partial class CyberpsychosisGlitchSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;

    private CyberpsychosisGlitchOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new();

        SubscribeLocalEvent<CyberpsychosisGlitchComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CyberpsychosisGlitchComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CyberpsychosisGlitchComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<CyberpsychosisGlitchComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttached(Entity<CyberpsychosisGlitchComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(Entity<CyberpsychosisGlitchComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnInit(Entity<CyberpsychosisGlitchComponent> ent, ref ComponentInit args)
    {
        if (_player.LocalEntity == ent.Owner)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnShutdown(Entity<CyberpsychosisGlitchComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity == ent.Owner)
            _overlayMan.RemoveOverlay(_overlay);
    }
}

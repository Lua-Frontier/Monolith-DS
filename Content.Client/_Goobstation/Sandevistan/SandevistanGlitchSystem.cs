using Content.Shared._Goobstation.Sandevistan;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._Goobstation.Sandevistan;

/// <summary>
/// Adds the Sandevistan glitch overlay for the local player while they have <see cref="SandevistanGlitchComponent"/>.
/// </summary>
public sealed partial class SandevistanGlitchSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;

    private SandevistanGlitchOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new();

        SubscribeLocalEvent<SandevistanGlitchComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SandevistanGlitchComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SandevistanGlitchComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SandevistanGlitchComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttached(Entity<SandevistanGlitchComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(Entity<SandevistanGlitchComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnInit(Entity<SandevistanGlitchComponent> ent, ref ComponentInit args)
    {
        if (_player.LocalEntity == ent.Owner)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnShutdown(Entity<SandevistanGlitchComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity == ent.Owner)
            _overlayMan.RemoveOverlay(_overlay);
    }
}

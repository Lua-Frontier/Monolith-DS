// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Goobstation.Client.MartialArts;

public sealed class MartialArtsSystem : SharedMartialArtsSystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private ComboIndicatorOverlay? _comboOverlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CanPerformComboComponent, ComponentStartup>(OnComboStartup);
        SubscribeLocalEvent<CanPerformComboComponent, ComponentShutdown>(OnComboShutdown);
        SubscribeLocalEvent<CanPerformComboComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<CanPerformComboComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        RemoveOverlay();
    }

    private void OnComboStartup(Entity<CanPerformComboComponent> ent, ref ComponentStartup args)
    {
        if (_player.LocalEntity == ent.Owner)
            AddOverlay();
    }

    private void OnComboShutdown(Entity<CanPerformComboComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity == ent.Owner)
            RemoveOverlay();
    }

    private void OnPlayerAttached(Entity<CanPerformComboComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        AddOverlay();
    }

    private void OnPlayerDetached(Entity<CanPerformComboComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        RemoveOverlay();
    }

    /// <summary>
    /// The overlay is only up while the local player can do combos.
    /// </summary>
    private void AddOverlay()
    {
        if (_comboOverlay != null)
            return;

        _comboOverlay = new ComboIndicatorOverlay();
        _overlay.AddOverlay(_comboOverlay);
    }

    private void RemoveOverlay()
    {
        if (_comboOverlay == null)
            return;

        _overlay.RemoveOverlay(_comboOverlay);
        _comboOverlay = null;
    }
}

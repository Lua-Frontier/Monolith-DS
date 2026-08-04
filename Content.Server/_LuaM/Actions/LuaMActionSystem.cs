// SPDX-FileCopyrightText: 2026 LuaMonolith contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.DeadSpace.Necromorphs.Unitology.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._LuaM.Actions;
using Content.Shared.Audio;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Actions;

public sealed class LuaMActionSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RetractableItemActionComponent, OnRetractableItemActionEvent>(OnRetractableItem);
        SubscribeLocalEvent<TriggerActionEvent>(OnTriggerAction);
        SubscribeLocalEvent<ObeliskActionEvent>(OnSummonObelisk);
        SubscribeLocalEvent<ObeliskActivateActionEvent>(OnActivateObelisk);
    }

    private void OnRetractableItem(Entity<RetractableItemActionComponent> ent, ref OnRetractableItemActionEvent args)
    {
        if (ent.Comp.SpawnedEntity is { } existing && Exists(existing))
        {
            QueueDel(existing);
            ent.Comp.SpawnedEntity = null;
            _audio.PlayPredicted(ent.Comp.RetractSounds, args.Performer, args.Performer);
            args.Handled = true;
            return;
        }

        var item = Spawn(ent.Comp.SpawnedPrototype, Transform(args.Performer).Coordinates);
        if (!_hands.TryPickupAnyHand(args.Performer, item))
        {
            QueueDel(item);
            return;
        }

        ent.Comp.SpawnedEntity = item;
        _audio.PlayPredicted(ent.Comp.SummonSounds, args.Performer, args.Performer);
        args.Handled = true;
    }

    private void OnTriggerAction(TriggerActionEvent args)
    {
        if (args.Action.Comp.Container is not { } container)
            return;

        _trigger.Trigger(container, args.Performer);
        args.Handled = true;
    }

    private void OnSummonObelisk(ObeliskActionEvent args)
    {
        Spawn("StructureObelisk", Transform(args.Performer).Coordinates);
        args.Handled = true;
    }

    private void OnActivateObelisk(ObeliskActivateActionEvent args)
    {
        if (!TryComp<UnitologyObeliskComponent>(args.Target, out var obelisk))
            return;

        obelisk.TriggerAt = _timing.CurTime;
        args.Handled = true;
    }
}
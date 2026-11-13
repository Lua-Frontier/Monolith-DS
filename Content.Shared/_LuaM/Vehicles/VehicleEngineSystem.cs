/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._LuaM.Vehicles;

public sealed partial class VehicleEngineSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleEngineComponent, VehicleSeatsChangedEvent>(OnSeatsChanged);
        SubscribeLocalEvent<VehicleEngineComponent, VehicleCanMoveEvent>(OnCanMove);
        SubscribeLocalEvent<VehicleEngineComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        SubscribeLocalEvent<VehicleOccupantComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<VehicleEngineComponent>();
        while (query.MoveNext(out var uid, out var engine))
        {
            if (engine.Exiting is not { } exiting || now < engine.ExitAt)
                continue;

            engine.Exiting = null;
            Dirty(uid, engine);

            if (TryComp<BuckleComponent>(exiting, out var buckle) && buckle.BuckledTo == uid)
                _buckle.Unbuckle((exiting, buckle), null);
        }
    }

    public bool CanDrive(VehicleEngineComponent engine)
    {
        return engine.EngineOn && engine.Exiting == null && _timing.CurTime >= engine.ReadyAt;
    }

    private void OnSeatsChanged(Entity<VehicleEngineComponent> ent, ref VehicleSeatsChangedEvent args)
    {
        if (args.Role != VehicleSeatRole.Driver)
            return;

        if (args.Seated)
        {
            if (HasComp<ActorComponent>(args.Occupant))
                Start(ent);

            return;
        }

        if (ent.Comp.Exiting == args.Occupant)
        {
            ent.Comp.Exiting = null;
            Dirty(ent);
        }

        Stop(ent);
    }

    private void OnPlayerAttached(Entity<VehicleOccupantComponent> ent, ref PlayerAttachedEvent args)
    {
        if (ent.Comp.Role == VehicleSeatRole.Driver && TryComp<VehicleEngineComponent>(ent.Comp.Vehicle, out var engine))
            Start((ent.Comp.Vehicle, engine));
    }

    private void OnCanMove(Entity<VehicleEngineComponent> ent, ref VehicleCanMoveEvent args)
    {
        if (!CanDrive(ent.Comp))
            args.Cancelled = true;
    }

    private void OnUnstrapAttempt(Entity<VehicleEngineComponent> ent, ref UnstrapAttemptEvent args)
    {
        var occupant = args.Buckle.Owner;
        if (args.Cancelled || args.User != occupant ||
            !TryComp<VehicleSeatsComponent>(ent, out var seats) || seats.Driver != occupant)
        {
            return;
        }

        if (ent.Comp.Exiting == occupant)
        {
            args.Cancelled = true;
            return;
        }

        if (!ent.Comp.EngineOn)
            return;

        args.Cancelled = true;
        Stop(ent);
        ent.Comp.Exiting = occupant;
        ent.Comp.ExitAt = _timing.CurTime + GetLength(ent.Comp.StopSound) + ent.Comp.ExitDelay;
        Dirty(ent);
        _popup.PopupClient(Loc.GetString("luam-vehicle-engine-stopping"), ent, occupant);
    }

    // -- Уже работающий мотор повторно не заводим, иначе звук запуска ляжет поверх гула
    private void Start(Entity<VehicleEngineComponent> ent)
    {
        if (_net.IsClient || ent.Comp.EngineOn)
            return;

        ent.Comp.EngineOn = true;
        ent.Comp.Exiting = null;
        ent.Comp.ReadyAt = _timing.CurTime + GetLength(ent.Comp.StartSound);
        ent.Comp.StartStream = _audio.PlayPvs(ent.Comp.StartSound, ent)?.Entity;
        Dirty(ent);
    }

    private void Stop(Entity<VehicleEngineComponent> ent)
    {
        if (!ent.Comp.EngineOn)
            return;

        ent.Comp.EngineOn = false;
        Dirty(ent);

        if (_net.IsClient)
            return;

        ent.Comp.StartStream = _audio.Stop(ent.Comp.StartStream);
        _audio.PlayPvs(ent.Comp.StopSound, ent);
    }

    private TimeSpan GetLength(SoundSpecifier? sound)
    {
        return sound == null ? TimeSpan.Zero : _audio.GetAudioLength(_audio.ResolveSound(sound));
    }
}

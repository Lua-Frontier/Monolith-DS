/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared._LuaM.Weapons;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared._LuaM.Vehicles;

public sealed partial class VehicleTurretSystem : EntitySystem
{
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<VehicleTurretAimEvent>(OnAim);
        SubscribeLocalEvent<VehicleOccupantComponent, LuaMGetGunEvent>(OnGetGun);
        SubscribeLocalEvent<HardpointItemComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<VehicleTurretComponent, VehicleSwitchWeaponActionEvent>(OnSwitchWeapon);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<VehicleTurretComponent>();
        while (query.MoveNext(out var uid, out var turret))
        {
            var delta = Angle.ShortestDistance(turret.LocalRotation, turret.TargetLocalRotation);
            if (Math.Abs(delta.Degrees) < 0.01)
                continue;

            var step = Angle.FromDegrees(turret.RotationSpeedDegrees * frameTime);
            turret.LocalRotation = Math.Abs(delta.Theta) <= step.Theta
                ? turret.TargetLocalRotation
                : (turret.LocalRotation + step * Math.Sign(delta.Theta)).Reduced();

            Dirty(uid, turret);
        }
    }

    public Angle GetWorldRotation(EntityUid vehicle, VehicleTurretComponent turret)
    {
        return (_transform.GetWorldRotation(vehicle) + turret.LocalRotation).Reduced();
    }

    public void AimAt(Entity<VehicleTurretComponent> ent, MapCoordinates target)
    {
        var (pos, rot) = _transform.GetWorldPositionRotation(ent);
        var dir = target.Position - pos;
        if (dir.LengthSquared() < 0.0001f)
            return;

        ent.Comp.TargetLocalRotation = (dir.ToWorldAngle() - rot).Reduced();
        Dirty(ent);
    }

    public bool IsAligned(Entity<VehicleTurretComponent> ent, MapCoordinates target)
    {
        var (pos, _) = _transform.GetWorldPositionRotation(ent);
        var dir = target.Position - pos;
        if (dir.LengthSquared() < 0.0001f)
            return true;

        var diff = Angle.ShortestDistance(GetWorldRotation(ent, ent.Comp), dir.ToWorldAngle());
        return Math.Abs(diff.Degrees) <= ent.Comp.FireGraceDegrees;
    }

    private void OnAim(VehicleTurretAimEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryGetEntity(msg.Vehicle, out var vehicle) ||
            !TryComp<VehicleSeatsComponent>(vehicle, out var seats) || seats.Gunner != user ||
            !TryComp<VehicleTurretComponent>(vehicle, out var turret))
        {
            return;
        }

        var coords = GetCoordinates(msg.Coordinates);
        if (!coords.IsValid(EntityManager))
            return;

        AimAt((vehicle.Value, turret), _transform.ToMapCoordinates(coords));
    }

    public bool TryGetSelectedWeapon(EntityUid vehicle, VehicleTurretComponent turret, [NotNullWhen(true)] out EntityUid? weapon, [NotNullWhen(true)] out GunComponent? gun)
    {
        if (_hardpoints.TryGetWeapon(vehicle, turret.SelectedWeapon, out weapon, out gun))
            return true;

        var other = turret.SelectedWeapon == HardpointType.Cannon ? HardpointType.Launcher : HardpointType.Cannon;
        return _hardpoints.TryGetWeapon(vehicle, other, out weapon, out gun);
    }

    private void OnGetGun(Entity<VehicleOccupantComponent> ent, ref LuaMGetGunEvent args)
    {
        if (ent.Comp.Role != VehicleSeatRole.Gunner || !TryComp<VehicleTurretComponent>(ent.Comp.Vehicle, out var turret))
            return;

        if (TryGetSelectedWeapon(ent.Comp.Vehicle, turret, out var weapon, out _))
            args.Gun = weapon;
    }

    private bool TryGetTurretVehicle(EntityUid item, out Entity<VehicleTurretComponent> vehicle)
    {
        var current = Transform(item).ParentUid;
        for (var i = 0; i < 3 && current.IsValid(); i++)
        {
            if (TryComp<VehicleTurretComponent>(current, out var turret))
            {
                vehicle = (current, turret);
                return true;
            }

            current = Transform(current).ParentUid;
        }

        vehicle = default;
        return false;
    }

    private void OnAttemptShoot(Entity<HardpointItemComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled || !TryGetTurretVehicle(ent, out var vehicle))
            return;

        if (vehicle.Comp.RequiresDeployed &&
            (!TryComp<VehicleDeployableComponent>(vehicle, out var deploy) || !deploy.Deployed))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("luam-vehicle-turret-needs-deploy");
            return;
        }

        if (!TryComp<GunComponent>(ent, out var gun) || gun.ShootCoordinates is not { } coords)
            return;

        if (!IsAligned(vehicle, _transform.ToMapCoordinates(coords)))
            args.Cancelled = true;
    }

    private void OnSwitchWeapon(Entity<VehicleTurretComponent> ent, ref VehicleSwitchWeaponActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var next = ent.Comp.SelectedWeapon == HardpointType.Cannon ? HardpointType.Launcher : HardpointType.Cannon;
        if (!_hardpoints.TryGetWeapon(ent, next, out var weapon, out _))
        {
            _popup.PopupClient(Loc.GetString("luam-vehicle-turret-no-other-weapon"), ent, args.Performer);
            return;
        }

        ent.Comp.SelectedWeapon = next;
        Dirty(ent);
        _popup.PopupClient(Loc.GetString("luam-vehicle-turret-selected", ("weapon", weapon.Value)), ent, args.Performer);
    }
}

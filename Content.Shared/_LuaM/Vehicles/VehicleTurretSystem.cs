/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared._LuaM.Weapons;
using Content.Shared.ActionBlocker;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Player;

namespace Content.Shared._LuaM.Vehicles;

public sealed partial class VehicleTurretSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public const float PixelsPerMeter = 32f;
    public const double DefaultSpriteDirectionBias = -0.05;
    private const float HullExitMargin = 0.2f;
    private const float MaxAimDistance = 64f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<VehicleTurretAimEvent>(OnAim);
        SubscribeLocalEvent<VehicleOccupantComponent, LuaMGetGunEvent>(OnGetGun);
        SubscribeLocalEvent<VehicleOccupantComponent, LuaMGetShootOriginEvent>(OnOccupantShootOrigin);
        SubscribeLocalEvent<VehicleTurretComponent, LuaMGetShootOriginEvent>(OnTurretShootOrigin);
        SubscribeLocalEvent<HardpointItemComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<HardpointItemComponent, LuaMGetMuzzleFlashOriginEvent>(OnMuzzleFlashOrigin);
        SubscribeLocalEvent<VehicleTurretComponent, VehicleSwitchWeaponActionEvent>(OnSwitchWeapon);
        SubscribeLocalEvent<VehicleTurretComponent, EntParentChangedMessage>(OnParentChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // -- Клиент предсказывает только башню, за которой сидит сам: своя цель у него есть сразу, чужие ждут сервер
        if (_net.IsClient)
        {
            if (_player.LocalEntity is { } local &&
                TryComp<VehicleOccupantComponent>(local, out var occupant) &&
                TryComp<VehicleSeatsComponent>(occupant.Vehicle, out var seats) && seats.Gunner == local &&
                _actionBlocker.CanConsciouslyPerformAction(local) &&
                TryComp<VehicleTurretComponent>(occupant.Vehicle, out var own))
            {
                RotateTurret((occupant.Vehicle, own), Transform(occupant.Vehicle), frameTime);
            }

            return;
        }

        var query = EntityQueryEnumerator<VehicleTurretComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var turret, out var xform))
        {
            RotateTurret((uid, turret), xform, frameTime);
        }
    }

    private void RotateTurret(Entity<VehicleTurretComponent> ent, TransformComponent xform, float frameTime)
    {
        if (!TryGetAimRotation(ent, xform, out var target))
            return;

        var turret = ent.Comp;
        var delta = Angle.ShortestDistance(turret.Rotation, target);
        if (Math.Abs(delta.Degrees) < 0.01)
            return;

        var step = Angle.FromDegrees(turret.RotationSpeedDegrees * frameTime);
        turret.Rotation = Math.Abs(delta.Theta) <= step.Theta
            ? target.Reduced()
            : (turret.Rotation + step * Math.Sign(delta.Theta)).Reduced();

        Dirty(ent);
    }

    private bool TryGetAimRotation(Entity<VehicleTurretComponent> ent, TransformComponent xform, out Angle rotation)
    {
        rotation = default;
        if (ent.Comp.AimOffset is not { } offset)
            return false;

        var dir = offset - xform.LocalRotation.RotateVec(GetTurretPivot(ent));
        if (dir.LengthSquared() < 0.0001f)
            return false;

        rotation = dir.ToWorldAngle();
        return true;
    }

    public static Angle GetLocalRotation(VehicleTurretComponent turret, TransformComponent vehicleXform)
    {
        return (turret.Rotation - vehicleXform.LocalRotation).Reduced();
    }

    public Angle GetWorldRotation(EntityUid vehicle, VehicleTurretComponent turret)
    {
        return (GetParentRotation(Transform(vehicle)) + turret.Rotation).Reduced();
    }

    private Angle GetParentRotation(TransformComponent xform)
    {
        return _transform.GetWorldRotation(xform) - xform.LocalRotation;
    }

    public Vector2? GetAimOffset(EntityUid vehicle, MapCoordinates target)
    {
        var xform = Transform(vehicle);
        if (xform.MapID != target.MapId || target.MapId == MapId.Nullspace)
            return null;

        var (pos, rot) = _transform.GetWorldPositionRotation(xform);
        return (xform.LocalRotation - rot).RotateVec(target.Position - pos);
    }

    public void AimAt(Entity<VehicleTurretComponent> ent, MapCoordinates target)
    {
        if (GetAimOffset(ent, target) is { } offset)
            ent.Comp.AimOffset = offset;
    }

    private void OnParentChanged(Entity<VehicleTurretComponent> ent, ref EntParentChangedMessage args)
    {
        if (_net.IsClient || args.OldParent is not { } old || !old.IsValid() ||
            TerminatingOrDeleted(old) || TerminatingOrDeleted(ent))
        {
            return;
        }

        var parent = args.Transform.ParentUid;
        var delta = _transform.GetWorldRotation(old) - (parent.IsValid() ? _transform.GetWorldRotation(parent) : Angle.Zero);
        ent.Comp.Rotation = (ent.Comp.Rotation + delta).Reduced();
        if (ent.Comp.AimOffset is { } offset)
            ent.Comp.AimOffset = delta.RotateVec(offset);

        Dirty(ent);
    }

    public bool IsAligned(Entity<VehicleTurretComponent> ent, MapCoordinates target)
    {
        var (pos, rot) = _transform.GetWorldPositionRotation(ent);
        var dir = target.Position - (pos + rot.RotateVec(GetTurretPivot(ent)));
        if (dir.LengthSquared() < 0.0001f)
            return true;

        var diff = Angle.ShortestDistance(GetWorldRotation(ent, ent.Comp), dir.ToWorldAngle());
        return Math.Abs(diff.Degrees) <= ent.Comp.FireGraceDegrees;
    }

    // -- Точка выстрела от клиента и прицел башни приходят разными сообщениями и на скорости расходятся на градусы:
    // -- готовность сверяем с собственной целью башни, а снаряд пускаем по стволу
    private bool IsOnAim(Entity<VehicleTurretComponent> ent, out bool hasAim)
    {
        hasAim = TryGetAimRotation(ent, Transform(ent), out var target);
        return hasAim && Math.Abs(Angle.ShortestDistance(ent.Comp.Rotation, target).Degrees) <= ent.Comp.FireGraceDegrees;
    }

    private EntityCoordinates GetBarrelTarget(Entity<VehicleTurretComponent> ent, EntityCoordinates muzzle, EntityCoordinates target)
    {
        var muzzleMap = _transform.ToMapCoordinates(muzzle);
        var targetMap = _transform.ToMapCoordinates(target);
        var distance = targetMap.MapId == muzzleMap.MapId
            ? MathF.Max((targetMap.Position - muzzleMap.Position).Length(), 1f)
            : MaxAimDistance;

        var barrel = GetWorldRotation(ent, ent.Comp).ToWorldVec();
        return _transform.ToCoordinates(new MapCoordinates(muzzleMap.Position + barrel * distance, muzzleMap.MapId));
    }

    public static Vector2 GetTurretPivot(VehicleTurretComponent turret, Direction hullDir)
    {
        var offset = turret.PixelOffsets.TryGetValue(hullDir, out var px) ? px / PixelsPerMeter : Vector2.Zero;
        return (-hullDir.ToAngle()).RotateVec(offset);
    }

    private Vector2 GetTurretPivot(Entity<VehicleTurretComponent> ent)
    {
        return GetTurretPivot(ent.Comp, GetSpriteDirection(GetHullScreenRotation(ent)));
    }

    private Angle GetHullScreenRotation(EntityUid vehicle)
    {
        var xform = Transform(vehicle);
        return xform.ParentUid == xform.GridUid ? xform.LocalRotation : _transform.GetWorldRotation(xform);
    }

    // -- Тот же выбор ракурса, что у движка для 4-направленного спрайта, вместе с его смещением у диагоналей
    public static Direction GetSpriteDirection(Angle screenAngle, double bias = DefaultSpriteDirectionBias)
    {
        var theta = screenAngle.Reduced().FlipPositive().Theta;
        var mod = Math.Floor(theta / MathHelper.PiOver2) % 2 - 0.5;
        var index = (int) Math.Round((theta + mod * bias) / MathHelper.PiOver2) % 4;
        return index switch
        {
            1 => Direction.East,
            2 => Direction.North,
            3 => Direction.West,
            _ => Direction.South,
        };
    }

    // -- Башня рисуется 4-направленным спрайтом: ракурс выбирается по экранному углу башни, остаток угла доворачивает картинку
    private Vector2 GetMuzzleLocal(Entity<VehicleTurretComponent> ent, EntityUid weapon)
    {
        var pivot = GetTurretPivot(ent);
        var local = GetLocalRotation(ent.Comp, Transform(ent));
        if (TryComp<HardpointItemComponent>(weapon, out var item) && item.MuzzleOffsets.Count > 0)
        {
            var dir = GetSpriteDirection(GetHullScreenRotation(ent) + local);
            if (item.MuzzleOffsets.TryGetValue(dir, out var px))
                return pivot + (local - dir.ToAngle()).RotateVec(px / PixelsPerMeter);
        }

        return pivot + local.ToWorldVec() * ent.Comp.MuzzleLength;
    }

    private EntityCoordinates GetMuzzle(Entity<VehicleTurretComponent> ent, EntityUid weapon)
    {
        var (pos, rot) = _transform.GetWorldPositionRotation(ent);
        var local = GetMuzzleLocal(ent, weapon);
        return _transform.ToCoordinates(new MapCoordinates(pos + rot.RotateVec(local), Transform(ent).MapID));
    }

    private void OnMuzzleFlashOrigin(Entity<HardpointItemComponent> ent, ref LuaMGetMuzzleFlashOriginEvent args)
    {
        if (args.Origin != null || !TryGetTurretVehicle(ent, out var turret))
            return;

        args.Origin = new EntityCoordinates(turret, GetMuzzleLocal(turret, ent));
    }

    private void OnTurretShootOrigin(Entity<VehicleTurretComponent> ent, ref LuaMGetShootOriginEvent args)
    {
        if (args.Origin != null || !TryGetTurretVehicle(args.Gun, out var vehicle) || vehicle.Owner != ent.Owner)
            return;

        var muzzle = GetMuzzle(ent, args.Gun);
        args.Origin = muzzle;
        args.Target = GetBarrelTarget(ent, muzzle, args.Target);
    }

    private void OnOccupantShootOrigin(Entity<VehicleOccupantComponent> ent, ref LuaMGetShootOriginEvent args)
    {
        if (args.Origin != null)
            return;

        var vehicle = ent.Comp.Vehicle;
        if (IsGunner(ent) &&
            TryGetTurretVehicle(args.Gun, out var turret) && turret.Owner == vehicle)
        {
            var muzzle = GetMuzzle(turret, args.Gun);
            args.Origin = muzzle;
            args.Target = GetBarrelTarget(turret, muzzle, args.Target);
            return;
        }

        args.Origin = GetHullExit(vehicle, ent, args.Target);
    }

    private EntityCoordinates? GetHullExit(EntityUid vehicle, EntityUid shooter, EntityCoordinates target)
    {
        if (!TryComp<FixturesComponent>(vehicle, out var fixtures) ||
            !GridVehicleMoverSystem.TryGetLocalBounds(fixtures, out var bounds))
        {
            return null;
        }

        var xform = Transform(vehicle);
        var targetMap = _transform.ToMapCoordinates(target);
        if (targetMap.MapId != xform.MapID)
            return null;

        var (pos, rot) = _transform.GetWorldPositionRotation(xform);
        var shooterPos = _transform.GetWorldPosition(shooter);
        var dir = (-rot).RotateVec(targetMap.Position - shooterPos);
        if (dir.LengthSquared() < 0.0001f)
            return null;

        dir = Vector2.Normalize(dir);
        var local = (-rot).RotateVec(shooterPos - pos);
        var distance = MathF.Min(
            ExitDistance(local.X, dir.X, bounds.Left, bounds.Right),
            ExitDistance(local.Y, dir.Y, bounds.Bottom, bounds.Top));

        var exit = local + dir * (MathF.Max(distance, 0f) + HullExitMargin);
        return _transform.ToCoordinates(new MapCoordinates(pos + rot.RotateVec(exit), xform.MapID));
    }

    private static float ExitDistance(float start, float dir, float min, float max)
    {
        if (dir > 0f)
            return (max - start) / dir;

        if (dir < 0f)
            return (min - start) / dir;

        return float.MaxValue;
    }

    private void OnAim(VehicleTurretAimEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryGetEntity(msg.Vehicle, out var vehicle) ||
            !TryComp<VehicleSeatsComponent>(vehicle, out var seats) || seats.Gunner != user ||
            !_actionBlocker.CanConsciouslyPerformAction(user) ||
            !TryComp<VehicleTurretComponent>(vehicle, out var turret))
        {
            return;
        }

        var offset = msg.Offset;
        if (!float.IsFinite(offset.X) || !float.IsFinite(offset.Y))
            return;

        if (offset.LengthSquared() > MaxAimDistance * MaxAimDistance)
            offset = Vector2.Normalize(offset) * MaxAimDistance;

        turret.AimOffset = offset;
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
        if (!IsGunner(ent) || !TryComp<VehicleTurretComponent>(ent.Comp.Vehicle, out var turret))
            return;

        if (TryGetSelectedWeapon(ent.Comp.Vehicle, turret, out var weapon, out _))
            args.Gun = weapon;
    }

    private bool IsGunner(Entity<VehicleOccupantComponent> ent)
    {
        return TryComp<VehicleSeatsComponent>(ent.Comp.Vehicle, out var seats) && seats.Gunner == ent.Owner;
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
        if (args.Cancelled)
            return;

        // -- Снятое с машины орудие из рук не стреляет
        if (!TryGetTurretVehicle(ent, out var vehicle))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("luam-vehicle-hardpoint-not-mounted");
            return;
        }

        if (vehicle.Comp.RequiresDeployed &&
            (!TryComp<VehicleDeployableComponent>(vehicle, out var deploy) || !deploy.Deployed))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("luam-vehicle-turret-needs-deploy");
            return;
        }

        var aligned = IsOnAim(vehicle, out var hasAim);
        if (!hasAim)
        {
            if (!TryComp<GunComponent>(ent, out var gun) || gun.ShootCoordinates is not { } coords)
                return;

            aligned = IsAligned(vehicle, _transform.ToMapCoordinates(coords));
        }

        if (!aligned)
        {
            args.Cancelled = true;
            args.KeepCooldown = true;
        }
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

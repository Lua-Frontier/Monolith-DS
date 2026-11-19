/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Client.Gameplay;
using Content.Shared._LuaM.Vehicles;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Buckle.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Vehicles;

public sealed partial class VehicleVisualsSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private static readonly EntProtoId InteriorVisual = "LuaMVehicleInteriorVisual";
    private static readonly EntProtoId TurretVisual = "LuaMVehicleTurretVisual";
    private const float TurretVisualCatchUp = 1.5f;
    private const float TurretVisualSnapDegrees = 45f;
    private const float TurretFadedAlpha = 0.6f;
    private const float HoverDelay = 0.15f;
    private const float HoverSearchRange = 5f;

    private readonly Dictionary<EntityUid, VisualState> _states = new();
    private readonly Dictionary<EntityUid, VisualState> _riderStates = new();
    private readonly HashSet<EntityUid> _hovered = new();

    // -- Пассажиров прячем под непрозрачным кузовом и сдвигаем их спрайты к сиденьям текущего ракурса
    private readonly Dictionary<EntityUid, Vector2> _shifted = new();
    private readonly HashSet<EntityUid> _hidden = new();
    private readonly HashSet<EntityUid> _seen = new();
    private readonly List<EntityUid> _restore = new();

    private sealed class VisualState
    {
        public EntityUid? Interior;
        public EntityUid? Overlay;
        public EntityUid? Turret;
        public float Fade;
        public float HoverTime;
        public float LeaveTime;
        public Angle TurretRotation;
        public string? TurretBase;
        public string? TurretWeapon;
    }

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(TransformSystem));

        SubscribeLocalEvent<VehicleInteriorComponent, ComponentStartup>(OnInteriorStartup);
        SubscribeLocalEvent<VehicleInteriorComponent, ComponentShutdown>(OnInteriorShutdown);
        SubscribeLocalEvent<VehicleRiderVisualsComponent, ComponentStartup>(OnRiderStartup);
        SubscribeLocalEvent<VehicleRiderVisualsComponent, ComponentShutdown>(OnRiderShutdown);
        SubscribeLocalEvent<HardpointSlotsComponent, HardpointsChangedEvent>(OnHardpointsChanged);
        SubscribeLocalEvent<HardpointSlotsComponent, ComponentStartup>(OnHardpointsStartup);
    }

    private void OnInteriorStartup(Entity<VehicleInteriorComponent> ent, ref ComponentStartup args)
    {
        var state = new VisualState();
        _states[ent] = state;

        var coords = new EntityCoordinates(ent, Vector2.Zero);
        var interior = Spawn(InteriorVisual, coords);
        if (TryComp<SpriteComponent>(interior, out var interiorSprite))
        {
            _sprite.LayerSetRsi((interior, interiorSprite), 0, new ResPath(ent.Comp.Sprite), ent.Comp.State);
            _sprite.SetVisible((interior, interiorSprite), false);
        }

        state.Interior = interior;

        if (HasComp<VehicleTurretComponent>(ent))
            state.Turret = Spawn(TurretVisual, coords);
    }

    // -- При удалении машины дочерние визуалы уже удалены вместе с ней, повторно их не трогаем
    private void OnInteriorShutdown(Entity<VehicleInteriorComponent> ent, ref ComponentShutdown args)
    {
        if (!_states.Remove(ent, out var state))
            return;

        if (state.Interior is { } interior && !TerminatingOrDeleted(interior))
            QueueDel(interior);

        if (state.Turret is { } turret && !TerminatingOrDeleted(turret))
            QueueDel(turret);
    }

    private void OnRiderStartup(Entity<VehicleRiderVisualsComponent> ent, ref ComponentStartup args)
    {
        var hasTurret = HasComp<VehicleTurretComponent>(ent);
        if (ent.Comp.OverlayVisual == null && ent.Comp.InteriorVisual == null && !hasTurret)
            return;

        var state = new VisualState();
        _riderStates[ent] = state;
        var coords = new EntityCoordinates(ent, Vector2.Zero);

        if (ent.Comp.OverlayVisual is { } overlayProto)
            state.Overlay = Spawn(overlayProto, coords);

        if (ent.Comp.InteriorVisual is { } interiorProto)
        {
            var interior = Spawn(interiorProto, coords);
            if (TryComp<SpriteComponent>(interior, out var interiorSprite))
                _sprite.SetVisible((interior, interiorSprite), false);

            state.Interior = interior;
        }

        if (hasTurret)
            state.Turret = Spawn(TurretVisual, coords);
    }

    private void OnRiderShutdown(Entity<VehicleRiderVisualsComponent> ent, ref ComponentShutdown args)
    {
        if (!_riderStates.Remove(ent, out var state))
            return;

        if (state.Overlay is { } overlay && !TerminatingOrDeleted(overlay))
            QueueDel(overlay);

        if (state.Interior is { } interior && !TerminatingOrDeleted(interior))
            QueueDel(interior);

        if (state.Turret is { } turret && !TerminatingOrDeleted(turret))
            QueueDel(turret);
    }

    private void OnHardpointsStartup(Entity<HardpointSlotsComponent> ent, ref ComponentStartup args)
    {
        UpdateHullLayers(ent);
    }

    private void OnHardpointsChanged(Entity<HardpointSlotsComponent> ent, ref HardpointsChangedEvent args)
    {
        UpdateHullLayers(ent);
    }

    private void UpdateHullLayers(Entity<HardpointSlotsComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        foreach (var slot in ent.Comp.Slots)
        {
            if (slot.VisualLayer is not { } layerKey || !_sprite.LayerMapTryGet((ent, sprite), layerKey, out var layer, false))
                continue;

            var item = _hardpoints.GetSlotItem(ent, slot.Id);
            if (item != null && TryComp<HardpointItemComponent>(item, out var hardpoint) && hardpoint.VisualState is { } state)
            {
                _sprite.LayerSetRsiState((ent, sprite), layer, state);
                _sprite.LayerSetVisible((ent, sprite), layer, true);
            }
            else
            {
                _sprite.LayerSetVisible((ent, sprite), layer, false);
            }
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_states.Count > 0 || _riderStates.Count > 0)
            UpdateHovered();

        var eyeRot = _eye.CurrentEye.Rotation;

        var query = EntityQueryEnumerator<VehicleInteriorComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var interior, out var sprite, out var xform))
        {
            if (!_states.TryGetValue(uid, out var state))
                continue;

            var shown = UpdateFade(uid, sprite, state, interior.FadeRate, interior.FadedAlpha, frameTime);

            var (worldPos, worldRot) = _transform.GetWorldPositionRotation(xform);
            var hullDir = VehicleTurretSystem.GetSpriteDirection(worldRot + eyeRot, SpriteComponent.DirectionBias);

            if (state.Turret is { } turretEnt)
                UpdateTurret(uid, xform, worldRot, state, turretEnt, hullDir, MathHelper.Lerp(1f, TurretFadedAlpha, state.Fade), frameTime);

            if (TryComp<StrapComponent>(uid, out var strap) && strap.BuckledEntities.Count > 0)
                UpdateOccupants(interior, strap, shown, worldPos, GetImageRotation(sprite, worldRot, hullDir, eyeRot), hullDir, eyeRot);
        }

        UpdateRiders(eyeRot, frameTime);
        RestoreUnseen();
    }

    // -- Пока машина поворачивается, её силуэт пробегает под курсором: короткие касания не должны мигать прозрачностью
    private bool UpdateFade(EntityUid uid, SpriteComponent sprite, VisualState state, float fadeRate, float fadedAlpha, float frameTime)
    {
        if (_hovered.Contains(uid))
        {
            state.HoverTime += frameTime;
            state.LeaveTime = 0f;
        }
        else if ((state.LeaveTime += frameTime) >= HoverDelay)
        {
            state.HoverTime = 0f;
        }

        var target = state.HoverTime >= HoverDelay ? 1f : 0f;
        state.Fade = target > state.Fade
            ? MathF.Min(target, state.Fade + fadeRate * frameTime)
            : MathF.Max(target, state.Fade - fadeRate * frameTime);

        var hullAlpha = MathHelper.Lerp(1f, fadedAlpha, state.Fade);
        if (!MathHelper.CloseTo(sprite.Color.A, hullAlpha))
            _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(hullAlpha));

        if (state.Overlay is { } overlay && TryComp<SpriteComponent>(overlay, out var overlaySprite) &&
            !MathHelper.CloseTo(overlaySprite.Color.A, hullAlpha))
        {
            _sprite.SetColor((overlay, overlaySprite), overlaySprite.Color.WithAlpha(hullAlpha));
        }

        // -- Интерьер совпадает с силуэтом кузова, поэтому сразу непрозрачный под ним: иначе при наведении мелькает пустой пол
        var shown = state.Fade > 0f;
        if (state.Interior is { } interiorEnt && TryComp<SpriteComponent>(interiorEnt, out var interiorSprite) &&
            interiorSprite.Visible != shown)
        {
            _sprite.SetColor((interiorEnt, interiorSprite), interiorSprite.Color.WithAlpha(1f));
            _sprite.SetVisible((interiorEnt, interiorSprite), shown);
        }

        return shown;
    }

    private static Angle GetImageRotation(SpriteComponent sprite, Angle worldRot, Direction hullDir, Angle eyeRot)
    {
        return sprite.NoRotation ? -eyeRot : worldRot - hullDir.ToAngle();
    }

    private void UpdateOccupants(
        VehicleInteriorComponent interior,
        StrapComponent strap,
        bool shown,
        Vector2 hullPos,
        Angle imageRot,
        Direction hullDir,
        Angle eyeRot)
    {
        interior.SeatOffsets.TryGetValue(hullDir, out var seats);

        foreach (var occupant in strap.BuckledEntities)
        {
            if (!TryComp<SpriteComponent>(occupant, out var sprite))
                continue;

            _seen.Add(occupant);

            if (!shown)
            {
                if (_hidden.Add(occupant))
                    _sprite.SetVisible((occupant, sprite), false);
            }
            else if (_hidden.Remove(occupant))
            {
                _sprite.SetVisible((occupant, sprite), true);
            }

            ShiftToSeat(occupant, sprite, seats, hullPos, imageRot, eyeRot);
        }
    }

    private void ShiftToSeat(EntityUid occupant, SpriteComponent sprite, List<Vector2>? seats, Vector2 hullPos, Angle imageRot, Angle eyeRot)
    {
        if (seats == null ||
            !TryComp<VehicleOccupantComponent>(occupant, out var seat) ||
            seat.Seat < 0 || seat.Seat >= seats.Count)
        {
            return;
        }

        if (!_shifted.TryGetValue(occupant, out var original))
        {
            original = sprite.Offset;
            _shifted[occupant] = original;
        }

        var target = hullPos + imageRot.RotateVec(seats[seat.Seat] / VehicleTurretSystem.PixelsPerMeter);
        var (occupantPos, occupantRot) = _transform.GetWorldPositionRotation(occupant);
        var spriteRot = sprite.NoRotation ? -eyeRot : occupantRot;
        var offset = original + (-spriteRot).RotateVec(target - occupantPos);
        if (!sprite.Offset.EqualsApprox(offset, 0.001))
            _sprite.SetOffset((occupant, sprite), offset);
    }

    private void UpdateRiders(Angle eyeRot, float frameTime)
    {
        var query = EntityQueryEnumerator<VehicleRiderVisualsComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var riders, out var sprite, out var xform))
        {
            var (worldPos, worldRot) = _transform.GetWorldPositionRotation(xform);
            var hullDir = VehicleTurretSystem.GetSpriteDirection(worldRot + eyeRot, SpriteComponent.DirectionBias);
            var depth = (int) (hullDir is Direction.East or Direction.West ? riders.SideDrawDepth : riders.FrontDrawDepth);
            if (sprite.DrawDepth != depth)
                _sprite.SetDrawDepth((uid, sprite), depth);

            if (_riderStates.TryGetValue(uid, out var state))
            {
                if (state.Interior is { } interiorEnt)
                {
                    UpdateFade(uid, sprite, state, riders.FadeRate, riders.FadedAlpha, frameTime);
                    if (TryComp<SpriteComponent>(interiorEnt, out var interiorSprite) && interiorSprite.DrawDepth != depth - 1)
                        _sprite.SetDrawDepth((interiorEnt, interiorSprite), depth - 1);
                }

                if (state.Turret is { } turretEnt)
                    UpdateTurret(uid, xform, worldRot, state, turretEnt, hullDir, MathHelper.Lerp(1f, TurretFadedAlpha, state.Fade), frameTime);
            }

            if (!TryComp<StrapComponent>(uid, out var strap) || strap.BuckledEntities.Count == 0)
                continue;

            riders.SeatOffsets.TryGetValue(hullDir, out var seats);
            var imageRot = GetImageRotation(sprite, worldRot, hullDir, eyeRot);
            foreach (var occupant in strap.BuckledEntities)
            {
                if (!TryComp<SpriteComponent>(occupant, out var occupantSprite))
                    continue;

                _seen.Add(occupant);
                ShiftToSeat(occupant, occupantSprite, seats, worldPos, imageRot, eyeRot);
            }
        }
    }

    private void RestoreUnseen()
    {
        _restore.Clear();
        foreach (var occupant in _hidden)
        {
            if (!_seen.Contains(occupant))
                _restore.Add(occupant);
        }

        foreach (var occupant in _restore)
        {
            _hidden.Remove(occupant);
            if (TryComp<SpriteComponent>(occupant, out var sprite))
                _sprite.SetVisible((occupant, sprite), true);
        }

        _restore.Clear();
        foreach (var occupant in _shifted.Keys)
        {
            if (!_seen.Contains(occupant))
                _restore.Add(occupant);
        }

        foreach (var occupant in _restore)
        {
            _shifted.Remove(occupant, out var original);
            if (TryComp<SpriteComponent>(occupant, out var sprite))
                _sprite.SetOffset((occupant, sprite), original);
        }

        _seen.Clear();
    }

    private void UpdateHovered()
    {
        _hovered.Clear();

        if (_ui.CurrentlyHovered is not IViewportControl vp ||
            !_input.MouseScreenPosition.IsValid ||
            _state.CurrentState is not GameplayStateBase gameplay)
        {
            return;
        }

        var coords = vp.PixelToMap(_input.MouseScreenPosition.Position);
        if (coords.MapId == MapId.Nullspace || !IsVehicleNear(coords))
            return;

        foreach (var ent in gameplay.GetClickableEntities(coords, false))
        {
            if (IsHoverable(ent))
                _hovered.Add(ent);
        }
    }

    // -- Поиск кликабельных сущностей под курсором дорогой, без машины рядом с курсором он не нужен
    private bool IsVehicleNear(MapCoordinates coords)
    {
        foreach (var uid in _states.Keys)
        {
            if (IsNear(uid, coords))
                return true;
        }

        foreach (var (uid, state) in _riderStates)
        {
            if (state.Interior != null && IsNear(uid, coords))
                return true;
        }

        return false;
    }

    private bool IsNear(EntityUid uid, MapCoordinates coords)
    {
        return TryComp(uid, out TransformComponent? xform) && xform.MapID == coords.MapId &&
               (_transform.GetWorldPosition(xform) - coords.Position).LengthSquared() <= HoverSearchRange * HoverSearchRange;
    }

    private bool IsHoverable(EntityUid uid)
    {
        return _states.ContainsKey(uid) || _riderStates.TryGetValue(uid, out var state) && state.Interior != null;
    }

    private void UpdateTurret(
        EntityUid vehicle,
        TransformComponent vehicleXform,
        Angle worldRot,
        VisualState state,
        EntityUid turretEnt,
        Direction hullDir,
        float alpha,
        float frameTime)
    {
        if (!TryComp<VehicleTurretComponent>(vehicle, out var turret) ||
            !TryComp<SpriteComponent>(turretEnt, out var sprite))
        {
            return;
        }

        string? baseState = null;
        string? weaponState = null;
        if (_hardpoints.GetSlotItem(vehicle, turret.TurretSlot) is { } turretItem)
        {
            baseState = CompOrNull<HardpointItemComponent>(turretItem)?.VisualState;
            foreach (var (_, slot, item) in _hardpoints.EnumerateSlots(turretItem))
            {
                if (item == null || CompOrNull<HardpointItemComponent>(item)?.VisualState is not { } visual)
                    continue;

                if (slot.Type == turret.SelectedWeapon)
                {
                    weaponState = visual;
                    break;
                }

                weaponState ??= visual;
            }
        }

        if (baseState != state.TurretBase || weaponState != state.TurretWeapon)
        {
            state.TurretBase = baseState;
            state.TurretWeapon = weaponState;
            var rsi = new ResPath(turret.Sprite);
            SetTurretLayer((turretEnt, sprite), 0, rsi, baseState);
            SetTurretLayer((turretEnt, sprite), 1, rsi, weaponState);
        }

        var target = worldRot - vehicleXform.LocalRotation + turret.Rotation;
        var delta = Angle.ShortestDistance(state.TurretRotation, target);
        var step = MathHelper.DegreesToRadians(turret.RotationSpeedDegrees * TurretVisualCatchUp) * frameTime;
        state.TurretRotation = Math.Abs(delta.Theta) <= step || Math.Abs(delta.Degrees) >= TurretVisualSnapDegrees
            ? target.Reduced()
            : (state.TurretRotation + new Angle(step * Math.Sign(delta.Theta))).Reduced();
        var localRotation = (state.TurretRotation - worldRot).Reduced();

        var localOffset = VehicleTurretSystem.GetTurretPivot(turret, hullDir);

        var xform = Transform(turretEnt);
        if (!xform.LocalPosition.EqualsApprox(localOffset))
            _transform.SetLocalPositionNoLerp(turretEnt, localOffset, xform);

        if (!xform.LocalRotation.EqualsApprox(localRotation))
            _transform.SetLocalRotationNoLerp(turretEnt, localRotation, xform);

        if (!MathHelper.CloseTo(sprite.Color.A, alpha))
            _sprite.SetColor((turretEnt, sprite), sprite.Color.WithAlpha(alpha));
    }

    private void SetTurretLayer(Entity<SpriteComponent> sprite, int layer, ResPath rsi, string? state)
    {
        if (state == null)
        {
            _sprite.LayerSetVisible(sprite.AsNullable(), layer, false);
            return;
        }

        _sprite.LayerSetRsi(sprite.AsNullable(), layer, rsi, state);
        _sprite.LayerSetVisible(sprite.AsNullable(), layer, true);
    }
}

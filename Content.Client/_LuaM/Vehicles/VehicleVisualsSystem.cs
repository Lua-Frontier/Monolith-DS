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
using Robust.Shared.Containers;
using Robust.Shared.Graphics.RSI;
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
    private const float PixelsPerMeter = 32f;
    private const float TurretVisualTurnSpeed = 6f;
    private const float TurretFadedAlpha = 0.6f;
    private const float HoverDelay = 0.15f;

    private readonly Dictionary<EntityUid, VisualState> _states = new();
    private readonly HashSet<EntityUid> _hovered = new();

    // -- Пассажиров прячем под непрозрачным кузовом и сдвигаем их спрайты к сиденьям текущего ракурса
    private readonly Dictionary<EntityUid, Vector2> _shifted = new();
    private readonly HashSet<EntityUid> _hidden = new();
    private readonly HashSet<EntityUid> _seen = new();
    private readonly List<EntityUid> _restore = new();

    private sealed class VisualState
    {
        public EntityUid? Interior;
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
        SubscribeLocalEvent<HardpointSlotsComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<HardpointSlotsComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
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

    private void OnHardpointsStartup(Entity<HardpointSlotsComponent> ent, ref ComponentStartup args)
    {
        UpdateHullLayers(ent);
    }

    private void OnContainerChanged<T>(Entity<HardpointSlotsComponent> ent, ref T args) where T : ContainerModifiedMessage
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

        if (_states.Count > 0)
            UpdateHovered();

        var eyeRot = _eye.CurrentEye.Rotation;

        var query = EntityQueryEnumerator<VehicleInteriorComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var interior, out var sprite, out var xform))
        {
            if (!_states.TryGetValue(uid, out var state))
                continue;

            // -- Пока машина поворачивается, её силуэт пробегает под курсором: короткие касания не должны мигать прозрачностью
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
                ? MathF.Min(target, state.Fade + interior.FadeRate * frameTime)
                : MathF.Max(target, state.Fade - interior.FadeRate * frameTime);

            var hullAlpha = MathHelper.Lerp(1f, interior.FadedAlpha, state.Fade);
            if (!MathHelper.CloseTo(sprite.Color.A, hullAlpha))
                _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(hullAlpha));

            // -- Интерьер совпадает с силуэтом кузова, поэтому сразу непрозрачный под ним: иначе при наведении мелькает пустой пол
            var shown = state.Fade > 0f;
            if (state.Interior is { } interiorEnt && TryComp<SpriteComponent>(interiorEnt, out var interiorSprite) &&
                interiorSprite.Visible != shown)
            {
                _sprite.SetColor((interiorEnt, interiorSprite), interiorSprite.Color.WithAlpha(1f));
                _sprite.SetVisible((interiorEnt, interiorSprite), shown);
            }

            var (worldPos, worldRot) = _transform.GetWorldPositionRotation(xform);
            var hullDir = GetHullDirection(worldRot + eyeRot);

            if (state.Turret is { } turretEnt)
                UpdateTurret(uid, state, turretEnt, hullDir, MathHelper.Lerp(1f, TurretFadedAlpha, state.Fade), frameTime);

            if (TryComp<StrapComponent>(uid, out var strap) && strap.BuckledEntities.Count > 0)
                UpdateOccupants(interior, strap, shown, worldPos, worldRot - hullDir.ToAngle(), hullDir, eyeRot);
        }

        RestoreUnseen();
    }

    // -- Тот же выбор ракурса, что у движка при отрисовке 4-направленного спрайта, со смещением у диагоналей
    private static Direction GetHullDirection(Angle screenAngle)
    {
        return SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, screenAngle.Reduced().FlipPositive()) switch
        {
            RsiDirection.North => Direction.North,
            RsiDirection.East => Direction.East,
            RsiDirection.West => Direction.West,
            _ => Direction.South,
        };
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

            if (seats == null ||
                !TryComp<VehicleOccupantComponent>(occupant, out var seat) ||
                seat.Seat < 0 || seat.Seat >= seats.Count)
            {
                continue;
            }

            if (!_shifted.TryGetValue(occupant, out var original))
            {
                original = sprite.Offset;
                _shifted[occupant] = original;
            }

            var target = hullPos + imageRot.RotateVec(seats[seat.Seat] / PixelsPerMeter);
            var (occupantPos, occupantRot) = _transform.GetWorldPositionRotation(occupant);
            var spriteRot = sprite.NoRotation ? -eyeRot : occupantRot;
            var offset = original + (-spriteRot).RotateVec(target - occupantPos);
            if (!sprite.Offset.EqualsApprox(offset, 0.001))
                _sprite.SetOffset((occupant, sprite), offset);
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
        if (coords.MapId == MapId.Nullspace)
            return;

        foreach (var ent in gameplay.GetClickableEntities(coords, false))
        {
            if (_states.ContainsKey(ent))
                _hovered.Add(ent);
        }
    }

    private void UpdateTurret(EntityUid vehicle, VisualState state, EntityUid turretEnt, Direction hullDir, float alpha, float frameTime)
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
            SetTurretLayer((turretEnt, sprite), 0, baseState);
            SetTurretLayer((turretEnt, sprite), 1, weaponState);
        }

        var delta = Angle.ShortestDistance(state.TurretRotation, turret.LocalRotation);
        var lerp = Math.Clamp(frameTime * TurretVisualTurnSpeed, 0f, 1f);
        state.TurretRotation = (state.TurretRotation + delta * lerp).Reduced();

        var offset = turret.PixelOffsets.TryGetValue(hullDir, out var px) ? px / PixelsPerMeter : Vector2.Zero;
        var localOffset = (-hullDir.ToAngle()).RotateVec(offset);

        var xform = Transform(turretEnt);
        _transform.SetLocalPositionNoLerp(turretEnt, localOffset, xform);
        _transform.SetLocalRotationNoLerp(turretEnt, state.TurretRotation, xform);

        if (!MathHelper.CloseTo(sprite.Color.A, alpha))
            _sprite.SetColor((turretEnt, sprite), sprite.Color.WithAlpha(alpha));
    }

    private void SetTurretLayer(Entity<SpriteComponent> sprite, int layer, string? state)
    {
        if (state == null)
        {
            _sprite.LayerSetVisible(sprite.AsNullable(), layer, false);
            return;
        }

        _sprite.LayerSetRsiState(sprite.AsNullable(), layer, state);
        _sprite.LayerSetVisible(sprite.AsNullable(), layer, true);
    }
}

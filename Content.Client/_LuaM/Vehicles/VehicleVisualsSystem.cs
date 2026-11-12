/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Shared._LuaM.Vehicles;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Buckle.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Vehicles;

public sealed partial class VehicleVisualsSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private static readonly EntProtoId InteriorVisual = "LuaMVehicleInteriorVisual";
    private static readonly EntProtoId TurretVisual = "LuaMVehicleTurretVisual";
    private const float PixelsPerMeter = 32f;
    private const float TurretVisualTurnSpeed = 6f;
    private const float TurretFadedAlpha = 0.6f;

    private readonly Dictionary<EntityUid, VisualState> _states = new();
    // -- Пассажиры, которых мы спрятали под непрозрачным кузовом, чтобы высокие спрайты не торчали наружу
    private readonly HashSet<EntityUid> _hidden = new();
    private readonly HashSet<EntityUid> _hideNow = new();
    private readonly List<EntityUid> _restore = new();

    private sealed class VisualState
    {
        public EntityUid? Interior;
        public EntityUid? Turret;
        public float Fade;
        public Angle TurretRotation;
        public string? TurretBase;
        public string? TurretWeapon;
    }

    public override void Initialize()
    {
        base.Initialize();

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
            _sprite.SetColor((interior, interiorSprite), Color.White.WithAlpha(0f));
        }

        state.Interior = interior;

        if (HasComp<VehicleTurretComponent>(ent))
            state.Turret = Spawn(TurretVisual, coords);
    }

    private void OnInteriorShutdown(Entity<VehicleInteriorComponent> ent, ref ComponentShutdown args)
    {
        if (!_states.Remove(ent, out var state))
            return;

        if (state.Interior is { } interior)
            QueueDel(interior);

        if (state.Turret is { } turret)
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

        var mouse = GetMouseMap();
        var local = _player.LocalEntity;
        var eyeRot = _eye.CurrentEye.Rotation;

        var query = EntityQueryEnumerator<VehicleInteriorComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var interior, out var sprite, out var xform))
        {
            if (!_states.TryGetValue(uid, out var state))
                continue;

            var (worldPos, worldRot) = _transform.GetWorldPositionRotation(xform);
            var inside = local != null && TryComp<VehicleOccupantComponent>(local, out var occupant) && occupant.Vehicle == uid;
            var hovered = mouse is { } m && m.MapId == xform.MapID && IsOverHull(uid, worldPos, worldRot, m.Position);

            var target = inside || hovered ? 1f : 0f;
            state.Fade = target > state.Fade
                ? MathF.Min(target, state.Fade + interior.FadeRate * frameTime)
                : MathF.Max(target, state.Fade - interior.FadeRate * frameTime);

            var hullAlpha = MathHelper.Lerp(1f, interior.FadedAlpha, state.Fade);
            if (!MathHelper.CloseTo(sprite.Color.A, hullAlpha))
                _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(hullAlpha));

            if (state.Interior is { } interiorEnt && TryComp<SpriteComponent>(interiorEnt, out var interiorSprite) &&
                !MathHelper.CloseTo(interiorSprite.Color.A, state.Fade))
            {
                _sprite.SetColor((interiorEnt, interiorSprite), interiorSprite.Color.WithAlpha(state.Fade));
            }

            if (state.Turret is { } turretEnt)
                UpdateTurret(uid, state, turretEnt, worldRot, eyeRot, MathHelper.Lerp(1f, TurretFadedAlpha, state.Fade), frameTime);

            if (state.Fade <= 0.01f && TryComp<StrapComponent>(uid, out var strap))
                _hideNow.UnionWith(strap.BuckledEntities);
        }

        foreach (var occupant in _hideNow)
        {
            if (occupant != local && _hidden.Add(occupant) && TryComp<SpriteComponent>(occupant, out var occupantSprite))
                _sprite.SetVisible((occupant, occupantSprite), false);
        }

        _restore.Clear();
        foreach (var occupant in _hidden)
        {
            if (!_hideNow.Contains(occupant) || occupant == local)
                _restore.Add(occupant);
        }

        foreach (var occupant in _restore)
        {
            _hidden.Remove(occupant);
            if (TryComp<SpriteComponent>(occupant, out var occupantSprite))
                _sprite.SetVisible((occupant, occupantSprite), true);
        }

        _hideNow.Clear();
    }

    private void UpdateTurret(EntityUid vehicle, VisualState state, EntityUid turretEnt, Angle worldRot, Angle eyeRot, float alpha, float frameTime)
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

        var hullDir = (worldRot + eyeRot).GetCardinalDir();
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

    private MapCoordinates? GetMouseMap()
    {
        if (_ui.CurrentlyHovered is not IViewportControl vp || !_input.MouseScreenPosition.IsValid)
            return null;

        return vp.PixelToMap(_input.MouseScreenPosition.Position);
    }

    private bool IsOverHull(EntityUid uid, Vector2 worldPos, Angle worldRot, Vector2 point)
    {
        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return false;

        var local = (-worldRot).RotateVec(point - worldPos);
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (fixture.Shape is PolygonShape poly && poly.ComputeAABB(Robust.Shared.Physics.Transform.Empty, 0).Contains(local))
                return true;
        }

        return false;
    }
}

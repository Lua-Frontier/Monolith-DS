/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 * Ported from Wolfgate (_WF/FtlEffects), AGPLv3.
 */

using System.Numerics;
using Content.Shared._LuaM.Shuttles.FtlEffects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.Timing;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._LuaM.Shuttles.FtlEffects;

public sealed partial class FtlDepartureSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IClientGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;

    private readonly HashSet<EntityUid> _present = new();
    private Overlay[]? _installed;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FtlDepartureComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FtlDepartureComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FtlDepartureComponent, EntParentChangedMessage>(OnParentChanged);
    }

    public override void Shutdown()
    {
        Uninstall();
        _present.Clear();
        base.Shutdown();
    }

    // -- Оверлеи живут, только пока на клиенте есть прыгающий грид вне nullspace: без прыжков их нет даже в списке отрисовки.
    // -- Ушедший из PVS грид клиент не удаляет, а уносит в nullspace вместе с компонентом, поэтому смотрим на карту
    private void OnStartup(Entity<FtlDepartureComponent> ent, ref ComponentStartup args)
    {
        Track(ent, Transform(ent).MapID != MapId.Nullspace);
    }

    private void OnShutdown(Entity<FtlDepartureComponent> ent, ref ComponentShutdown args)
    {
        Track(ent, false);
    }

    private void OnParentChanged(Entity<FtlDepartureComponent> ent, ref EntParentChangedMessage args)
    {
        Track(ent, args.Transform.MapID != MapId.Nullspace);
    }

    private void Track(EntityUid uid, bool present)
    {
        if (present ? !_present.Add(uid) : !_present.Remove(uid))
            return;
        if (_present.Count == 0)
            Uninstall();
        else
            Install();
    }

    private void Install()
    {
        if (_installed != null)
            return;
        var background = new FtlMotionBackgroundOverlay(this);
        _installed = [background, new FtlMotionOverlay(this, background), new FtlDepartureOverlay(this)];
        foreach (var overlay in _installed)
            _overlays.AddOverlay(overlay);
    }

    private void Uninstall()
    {
        if (_installed == null)
            return;
        foreach (var overlay in _installed)
        {
            _overlays.RemoveOverlay(overlay);
            overlay.Dispose();
        }
        _installed = null;
    }

    // -- Гриды не предиктятся, поэтому берём время показанного серверного состояния, а не предиктнутые часы
    public TimeSpan Now
    {
        get
        {
            var lead = Math.Max(0L, (long) _timing.CurTick.Value - _timing.LastRealTick.Value);
            return _timing.CurTime - TimeSpan.FromTicks(_timing.TickPeriod.Ticks * lead);
        }
    }

    // -- Камера экипажа остаётся на борту; рывок видят только наблюдатели снаружи
    public float Motion(EntityUid grid, FtlDepartureComponent effect)
    {
        if (Aboard(grid, effect))
            return 0f;
        return FtlDepartureTiming.Motion(Now, effect.Departure, effect.Entered, effect.Arriving);
    }

    public bool Gone(EntityUid grid, FtlDepartureComponent effect)
    {
        return !Aboard(grid, effect) && FtlDepartureTiming.Gone(Now, effect.Departure, effect.Entered, effect.Arriving);
    }

    public Matrix3x2 Moved(FtlDepartureComponent effect, MapGridComponent grid, TransformComponent xform, Matrix3x2 matrix,
        float motion)
    {
        if (effect.Lead is { } lead && TryComp(lead, out MapGridComponent? leadGrid) &&
            TryComp(lead, out TransformComponent? leadXform) && leadXform.MapID == xform.MapID)
        {
            return FtlConeGeometry.FollowerTransform(leadGrid.LocalAABB, motion, matrix,
                _transforms.GetWorldMatrix(leadXform));
        }
        return FtlConeGeometry.MotionTransform(grid.LocalAABB, motion) * matrix;
    }

    public bool HasMotion(MapId map, Box2 view)
    {
        var query = EntityQueryEnumerator<FtlDepartureComponent, MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var effect, out var grid, out var xform))
        {
            var motion = Motion(uid, effect);
            if (xform.MapID != map || MathF.Abs(motion) < 0.0001f && !Gone(uid, effect))
                continue;
            var matrix = _transforms.GetWorldMatrix(xform);
            var moved = Moved(effect, grid, xform, matrix, motion);
            if (view.Intersects(matrix.TransformBox(grid.LocalAABB.Enlarged(0.5f))) ||
                view.Intersects(moved.TransformBox(grid.LocalAABB.Enlarged(0.5f))))
                return true;
        }
        return false;
    }

    private bool Aboard(EntityUid grid, FtlDepartureComponent effect)
    {
        if (_player.LocalEntity is not { } player)
            return false;

        var viewed = TryComp(player, out EyeComponent? eye) && eye.Target is { } target && Exists(target)
            ? target
            : player;

        if (!TryComp(viewed, out TransformComponent? xform) || xform.GridUid is not { } deck)
            return false;
        var lead = effect.Lead ?? grid;
        return deck == grid || deck == lead || TryComp(deck, out FtlDepartureComponent? own) && own.Lead == lead;
    }
}

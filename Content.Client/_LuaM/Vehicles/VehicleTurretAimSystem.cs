/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Shared._LuaM.Vehicles;
using Content.Shared._LuaM.Vehicles.Components;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.Vehicles;

public sealed partial class VehicleTurretAimSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(0.08);

    private TimeSpan _nextSend;
    private Vector2 _lastSent;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_timing.RealTime < _nextSend ||
            _player.LocalEntity is not { } local ||
            !TryComp<VehicleOccupantComponent>(local, out var occupant) ||
            occupant.Role != VehicleSeatRole.Gunner ||
            !HasComp<VehicleTurretComponent>(occupant.Vehicle) ||
            !_input.MouseScreenPosition.IsValid)
        {
            return;
        }

        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mouse.MapId == MapId.Nullspace || (mouse.Position - _lastSent).LengthSquared() < 0.01f)
            return;

        _nextSend = _timing.RealTime + SendInterval;
        _lastSent = mouse.Position;

        var coords = _transform.ToCoordinates(mouse);
        RaiseNetworkEvent(new VehicleTurretAimEvent(GetNetEntity(occupant.Vehicle), GetNetCoordinates(coords)));
    }
}

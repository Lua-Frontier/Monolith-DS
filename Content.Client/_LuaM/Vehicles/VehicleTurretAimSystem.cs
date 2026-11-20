/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Shared._LuaM.Vehicles;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.ActionBlocker;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.Vehicles;

public sealed partial class VehicleTurretAimSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;

    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(0.03);

    private TimeSpan _nextSend;
    private Vector2? _lastSent;
    private EntityUid _lastVehicle;
    private int _lastSign;
    private int _pendingSign;
    private TimeSpan _reverseAt;

    // -- Курсор переводим в мир только после того, как камера встала на машину в этом кадре: иначе на скорости прицел
    // -- отстаёт на кадр пути, и башня дёргается вслед за неровным временем кадра
    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(EyeSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } local ||
            !TryComp<VehicleOccupantComponent>(local, out var occupant) ||
            !TryComp<VehicleSeatsComponent>(occupant.Vehicle, out var seats) || seats.Gunner != local ||
            !_actionBlocker.CanConsciouslyPerformAction(local) ||
            !TryComp<VehicleTurretComponent>(occupant.Vehicle, out var turret))
        {
            _lastVehicle = EntityUid.Invalid;
            return;
        }

        if (!_input.MouseScreenPosition.IsValid)
            return;

        var vehicle = occupant.Vehicle;
        if (vehicle != _lastVehicle)
        {
            _lastVehicle = vehicle;
            _lastSent = null;
            _lastSign = 0;
            _pendingSign = 0;
            turret.AimOffset = null;
        }

        if (_turret.GetAimOffset(vehicle, _eye.PixelToMap(_input.MouseScreenPosition)) is { } offset &&
            ShouldApply(turret, offset))
        {
            turret.AimOffset = offset;
        }

        if (turret.AimOffset is not { } aim || aim == _lastSent || _timing.RealTime < _nextSend)
            return;

        _nextSend = _timing.RealTime + SendInterval;
        _lastSent = aim;
        RaiseNetworkEvent(new VehicleTurretAimEvent(GetNetEntity(vehicle), aim));
    }

    // -- Мелкую дрожь курсора башня не отрабатывает, а смену направления поворота выжидает: иначе шум на ходу
    // -- превращается в качание башни туда-обратно
    private bool ShouldApply(VehicleTurretComponent turret, Vector2 offset)
    {
        if (turret.AimOffset is not { } current)
            return true;

        var delta = Angle.ShortestDistance(current.ToWorldAngle(), offset.ToWorldAngle()).Degrees;
        if (Math.Abs(delta) <= turret.AimDeadzoneDegrees)
            return false;

        var sign = Math.Sign(delta);
        if (_lastSign != 0 && sign != _lastSign && turret.AimReverseDelay > 0f)
        {
            if (_pendingSign != sign)
            {
                _pendingSign = sign;
                _reverseAt = _timing.RealTime + TimeSpan.FromSeconds(turret.AimReverseDelay);
            }

            if (_timing.RealTime < _reverseAt)
                return false;
        }

        _pendingSign = 0;
        _lastSign = sign;
        return true;
    }
}

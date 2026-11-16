using Content.Shared._LuaM.Vehicles.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._LuaM.Vehicles;

public sealed partial class HoverVehicleSystem : EntitySystem
{
    [Dependency] private GridVehicleMoverSystem _mover = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HoverVehicleComponent, GridVehicleMoverComponent, VehicleSeatsComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var hover, out var mover, out var seats, out var body))
        {
            if (!mover.Floating || seats.Driver is not { } driver)
                continue;

            if (_net.IsClient && (!_timing.InPrediction || driver != _player.LocalEntity || !body.Predict))
                continue;

            var (throttle, steering, brake) = _mover.GetInput(uid, seats);
            if (throttle == 0f && steering == 0f && !brake)
                continue;

            var xform = Transform(uid);
            var rotation = _transform.GetWorldRotation(xform);
            if (steering != 0f)
            {
                rotation += Angle.FromDegrees(steering * hover.RotationSpeedDegrees * frameTime);
                _transform.SetWorldRotation(uid, rotation);
            }

            var velocity = body.LinearVelocity;
            if (throttle != 0f)
                velocity += rotation.ToWorldVec() * throttle * hover.Thrust * frameTime;

            if (brake)
                velocity *= MathF.Max(0f, 1f - hover.BrakeRate * frameTime);

            var speed = velocity.Length();
            var maxSpeed = hover.MaxSpeed * mover.SpeedLimit;
            if (speed > maxSpeed)
                velocity *= maxSpeed / speed;

            _physics.SetLinearVelocity(uid, velocity, body: body);
        }
    }
}

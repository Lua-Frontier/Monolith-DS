using Content.Shared._LuaM.Vehicles.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._LuaM.Vehicles;

public sealed partial class HoverVehicleSystem : EntitySystem
{
    [Dependency] private GridVehicleMoverSystem _mover = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<HoverVehicleComponent, GridVehicleMoverComponent, VehicleSeatsComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var hover, out var mover, out var seats, out var body))
        {
            if (!mover.Floating || seats.Driver == null)
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
            if (speed > hover.MaxSpeed)
                velocity *= hover.MaxSpeed / speed;

            _physics.SetLinearVelocity(uid, velocity, body: body);
        }
    }
}

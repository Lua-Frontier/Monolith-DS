using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._LuaM.Vehicles;
using Content.Shared._LuaM.Buckle;
using Content.Shared._LuaM.Vehicles;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared._LuaM.Weapons;
using Content.Shared.Buckle;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class TurretAimProbeTest
{
    [Test]
    public async Task AimIsSampledAfterTheEyeMoves()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var sysMan = client.ResolveDependency<IEntitySystemManager>();
            var prop = sysMan.GetType().GetProperty("FrameUpdateOrder", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(prop, Is.Not.Null);
            var order = ((IEnumerable<System.Type>) prop!.GetValue(sysMan)!).ToList();
            var eye = order.IndexOf(typeof(EyeSystem));
            var aim = order.IndexOf(typeof(VehicleTurretAimSystem));
            TestContext.Out.WriteLine($"frame order: eye={eye} aim={aim} of {order.Count}");
            Assert.That(eye, Is.GreaterThanOrEqualTo(0));
            Assert.That(aim, Is.GreaterThan(eye));
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("LuaMVehicleSPPVanOneMan", 0)]
    [TestCase("LuaMVehicleTank", 1)]
    public async Task GunnerFiresOnTheMoveWithSkewedShotCoordinates(string proto, int gunnerSeat)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var hardpoints = server.System<HardpointSystem>();
        var turretSys = server.System<VehicleTurretSystem>();
        var gunSys = server.System<SharedGunSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();

        EntityUid vehicle = default;
        EntityUid gunner = default;

        await server.WaitPost(() =>
        {
            vehicle = entMan.SpawnEntity(proto, new MapCoordinates(Vector2.Zero, map.MapId));
            gunner = Seat(entMan, buckle, vehicle, gunnerSeat);
            turretSys.AimAt((vehicle, entMan.GetComponent<VehicleTurretComponent>(vehicle)), new MapCoordinates(new Vector2(6f, 0f), map.MapId));
        });

        await pair.RunTicksSync(timing.TickRate * 5);

        await server.WaitAssertion(() =>
        {
            Assert.That(hardpoints.TryGetWeapon(vehicle, HardpointType.Cannon, out var cannon, out var gun), Is.True);
            xformSys.SetWorldPosition(vehicle, new Vector2(0f, 3f));

            var skewed = new EntityCoordinates(map.MapUid, new Vector2(6f, 3.5f));
            var origin = new LuaMGetShootOriginEvent(cannon!.Value, skewed);
            entMan.EventBus.RaiseLocalEvent(gunner, ref origin);
            var shotAt = xformSys.ToMapCoordinates(origin.Target).Position;
            var muzzle = xformSys.ToMapCoordinates(origin.Origin!.Value).Position;
            var barrel = turretSys.GetWorldRotation(vehicle, entMan.GetComponent<VehicleTurretComponent>(vehicle));
            var off = Angle.ShortestDistance((shotAt - muzzle).ToWorldAngle(), barrel).Degrees;
            TestContext.Out.WriteLine($"{proto}: shot target {shotAt}, off barrel {off:F3} deg");
            Assert.That(Math.Abs(off), Is.LessThan(0.1));

            gunSys.AttemptShoot(gunner, cannon.Value, gun!, skewed);
            Assert.That(gun!.NextFire, Is.GreaterThan(timing.CurTime));
        });

        await pair.RunTicksSync(timing.TickRate * 8);

        await server.WaitAssertion(() =>
        {
            Assert.That(hardpoints.TryGetWeapon(vehicle, HardpointType.Cannon, out var cannon, out var gun), Is.True);
            Assert.That(gun!.NextFire, Is.LessThanOrEqualTo(timing.CurTime));

            var side = new MapCoordinates(new Vector2(0f, 9f), map.MapId);
            turretSys.AimAt((vehicle, entMan.GetComponent<VehicleTurretComponent>(vehicle)), side);
            gunSys.AttemptShoot(gunner, cannon!.Value, gun, new EntityCoordinates(map.MapUid, side.Position));
            Assert.That(gun.NextFire, Is.LessThanOrEqualTo(timing.CurTime));
            entMan.DeleteEntity(vehicle);
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid Seat(IEntityManager entMan, SharedBuckleSystem buckle, EntityUid vehicle, int seat)
    {
        var mob = entMan.SpawnEntity("MobHuman", entMan.GetComponent<TransformComponent>(vehicle).Coordinates);
        var slots = entMan.GetComponent<StrapSlotsComponent>(vehicle);
        slots.Requested[mob] = seat;
        Assert.That(buckle.TryBuckle(mob, mob, vehicle), Is.True);
        slots.Requested.Remove(mob);
        return mob;
    }
}

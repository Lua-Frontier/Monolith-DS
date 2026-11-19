using System.Collections.Generic;
using System.Numerics;
using Content.Shared._LuaM.Buckle;
using Content.Shared._LuaM.Vehicles;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared._LuaM.Weapons;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Gravity;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Standing;
using Content.Shared._White.Standing;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Pulling.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class HumveeTests
{
    [Test]
    public async Task BaseHumveeHasNoTurret()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var hardpoints = server.System<HardpointSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var vehicle = entMan.SpawnEntity("LuaMVehicleHumvee", new MapCoordinates(Vector2.Zero, map.MapId));
            Assert.That(hardpoints.GetSlotItem(vehicle, "turret"), Is.Null);
            Assert.That(hardpoints.TryGetWeapon(vehicle, HardpointType.Cannon, out _, out _), Is.False);
            entMan.DeleteEntity(vehicle);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("LuaMVehicleHumveeARC", 1, 4, 0.65f, 1.5f)]
    [TestCase("LuaMVehicleSPPVan", 1, 2, 0.6f, 1.5f)]
    [TestCase("LuaMVehicleTank", 2, 1, 0.8f, 2f)]
    public async Task ShotsStartAtMuzzleAndOutsideHull(string proto, int passengerSeat, int gunnerSeat, float halfWidth, float maxMuzzle)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var hardpoints = server.System<HardpointSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var vehicle = entMan.SpawnEntity(proto, new MapCoordinates(Vector2.Zero, map.MapId));
            var passenger = Seat(entMan, buckle, vehicle, passengerSeat);
            var gunner = Seat(entMan, buckle, vehicle, gunnerSeat);
            var target = new EntityCoordinates(map.MapUid, new Vector2(10f, 0f));

            var passengerEv = new LuaMGetShootOriginEvent(passenger, target);
            entMan.EventBus.RaiseLocalEvent(passenger, ref passengerEv);
            Assert.That(passengerEv.Origin, Is.Not.Null);
            var exit = xformSys.ToMapCoordinates(passengerEv.Origin!.Value).Position;
            Assert.That(exit.X, Is.GreaterThan(halfWidth));

            Assert.That(hardpoints.TryGetWeapon(vehicle, HardpointType.Cannon, out var cannon, out _), Is.True);
            var gunnerEv = new LuaMGetShootOriginEvent(cannon!.Value, target);
            entMan.EventBus.RaiseLocalEvent(gunner, ref gunnerEv);
            Assert.That(gunnerEv.Origin, Is.Not.Null);
            var muzzle = xformSys.ToMapCoordinates(gunnerEv.Origin!.Value).Position;
            Assert.That(muzzle, Is.Not.EqualTo(xformSys.GetWorldPosition(gunner)));
            Assert.That(muzzle.Length(), Is.LessThan(maxMuzzle));

            entMan.DeleteEntity(vehicle);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("LuaMVehicleTank", 0, false)]
    [TestCase("LuaMVehicleTank", 2, false)]
    [TestCase("LuaMVehicleTankOneMan", 0, false)]
    [TestCase("LuaMVehicleHumvee", 1, true)]
    public async Task OccupantHandsAndMelee(string proto, int seat, bool canMelee)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var hands = server.System<SharedHandsSystem>();
        var melee = server.System<SharedMeleeWeaponSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var vehicle = entMan.SpawnEntity(proto, new MapCoordinates(Vector2.Zero, map.MapId));
            var occupant = Seat(entMan, buckle, vehicle, seat);

            var free = 0;
            foreach (var hand in hands.EnumerateHands(occupant))
            {
                if (hand.HeldEntity == null)
                    free++;
            }

            Assert.That(free == 0, Is.EqualTo(!canMelee));
            Assert.That(melee.TryGetWeapon(occupant, out _, out _), Is.EqualTo(canMelee));

            entMan.DeleteEntity(vehicle);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TankMuzzleSitsOnDrawnBarrel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var hardpoints = server.System<HardpointSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var vehicle = entMan.SpawnEntity("LuaMVehicleTank", new MapCoordinates(Vector2.Zero, map.MapId));
            var gunner = Seat(entMan, buckle, vehicle, 1);
            var turret = entMan.GetComponent<VehicleTurretComponent>(vehicle);
            turret.Rotation = new Angle(MathF.PI / 2f);
            Assert.That(hardpoints.TryGetWeapon(vehicle, HardpointType.Cannon, out var cannon, out _), Is.True);

            // -- Корпус смотрит на юг (ось башни -1, 0 px), ствол на восток: в ракурсе East дуло нарисовано в 40, 6 px от центра башни
            var expected = new Vector2(39f, 6f) / 32f;

            var flash = new LuaMGetMuzzleFlashOriginEvent();
            entMan.EventBus.RaiseLocalEvent(cannon!.Value, ref flash);
            Assert.That(flash.Origin, Is.Not.Null);
            Assert.That(Vector2.Distance(xformSys.ToMapCoordinates(flash.Origin!.Value).Position, expected), Is.LessThan(0.01f));

            var shot = new LuaMGetShootOriginEvent(cannon.Value, new EntityCoordinates(map.MapUid, new Vector2(10f, 0.2f)));
            entMan.EventBus.RaiseLocalEvent(gunner, ref shot);
            Assert.That(shot.Origin, Is.Not.Null);
            Assert.That(Vector2.Distance(xformSys.ToMapCoordinates(shot.Origin!.Value).Position, expected), Is.LessThan(0.01f));

            entMan.DeleteEntity(vehicle);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TurretHoldsAimWhileHullMovesAndTurns()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var turretSys = server.System<VehicleTurretSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();

        EntityUid vehicle = default;
        Angle aimed = default;

        await server.WaitPost(() =>
        {
            vehicle = entMan.SpawnEntity("LuaMVehicleTank", new MapCoordinates(Vector2.Zero, map.MapId));
            var turret = entMan.GetComponent<VehicleTurretComponent>(vehicle);
            turretSys.AimAt((vehicle, turret), new MapCoordinates(new Vector2(6f, 0f), map.MapId));
        });

        await pair.RunTicksSync(timing.TickRate * 2);

        await server.WaitPost(() =>
        {
            aimed = turretSys.GetWorldRotation(vehicle, entMan.GetComponent<VehicleTurretComponent>(vehicle));
            Assert.That(Math.Abs(Angle.ShortestDistance(aimed, new Vector2(1f, 0f).ToWorldAngle()).Degrees), Is.LessThan(2));
            xformSys.SetWorldPositionRotation(vehicle, new Vector2(0f, 3f), new Angle(MathF.PI / 2f));
        });

        await pair.RunTicksSync(timing.TickRate);

        await server.WaitAssertion(() =>
        {
            var turret = entMan.GetComponent<VehicleTurretComponent>(vehicle);
            Assert.That(Math.Abs(Angle.ShortestDistance(turretSys.GetWorldRotation(vehicle, turret), aimed).Degrees), Is.LessThan(3));
            Assert.That(turretSys.IsAligned((vehicle, turret), new MapCoordinates(new Vector2(6f, 3f), map.MapId)), Is.True);
            entMan.DeleteEntity(vehicle);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TurretKeepsWorldAimWhenVehicleChangesParent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var turretSys = server.System<VehicleTurretSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();

        EntityUid vehicle = default;
        MapCoordinates target = default;

        await server.WaitPost(() =>
        {
            xformSys.SetLocalRotation(map.Grid, new Angle(MathF.PI / 2f));
            vehicle = entMan.SpawnEntity("LuaMVehicleTank", map.GridCoords);
            var turret = entMan.GetComponent<VehicleTurretComponent>(vehicle);
            target = new MapCoordinates(xformSys.GetWorldPosition(vehicle) + new Vector2(6f, 0f), map.MapId);
            turretSys.AimAt((vehicle, turret), target);
        });

        await pair.RunTicksSync(timing.TickRate * 2);

        await server.WaitPost(() =>
        {
            Assert.That(turretSys.IsAligned((vehicle, entMan.GetComponent<VehicleTurretComponent>(vehicle)), target), Is.True);
            xformSys.SetParent(vehicle, map.MapUid);
        });

        await pair.RunTicksSync(timing.TickRate);

        await server.WaitAssertion(() =>
        {
            var turret = entMan.GetComponent<VehicleTurretComponent>(vehicle);
            Assert.That(entMan.GetComponent<TransformComponent>(vehicle).ParentUid, Is.EqualTo(map.MapUid));
            Assert.That(turretSys.IsAligned((vehicle, turret), target), Is.True);
            entMan.DeleteEntity(vehicle);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("MobSyndicateNavalOperatorVoid")]
    [TestCase("MobVoidBaseTSFD")]
    public async Task ArcTurretFiresAtHostiles(string hostile)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var hardpoints = server.System<HardpointSystem>();
        var buckle = server.System<SharedBuckleSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();

        EntityUid vehicle = default;
        EntityUid cannon = default;
        var before = 0;

        await server.WaitAssertion(() =>
        {
            vehicle = entMan.SpawnEntity("LuaMVehicleHumveeARC", new MapCoordinates(Vector2.Zero, map.MapId));
            var deploy = entMan.GetComponent<VehicleDeployableComponent>(vehicle);
            deploy.Deployed = true;
            entMan.Dirty(vehicle, deploy);
            Seat(entMan, buckle, vehicle, 0);

            Assert.That(hardpoints.TryGetWeapon(vehicle, HardpointType.Cannon, out var weapon, out _), Is.True);
            cannon = weapon!.Value;
            before = entMan.GetComponent<BallisticAmmoProviderComponent>(cannon).Count;

            entMan.SpawnEntity(hostile, new MapCoordinates(new Vector2(4f, 0f), map.MapId));
        });

        await pair.RunTicksSync(timing.TickRate * 3);

        await server.WaitAssertion(() =>
        {
            var after = entMan.GetComponent<BallisticAmmoProviderComponent>(cannon).Count;
            Assert.That(after, Is.LessThan(before));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DriverExitsAfterEngineStops()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();

        EntityUid vehicle = default;
        EntityUid driver = default;
        TimeSpan exitDelay = default;

        await server.WaitPost(() =>
        {
            vehicle = entMan.SpawnEntity("LuaMVehicleHumvee", new MapCoordinates(Vector2.Zero, map.MapId));
            driver = Seat(entMan, buckle, vehicle, 0);
        });

        await pair.RunTicksSync(timing.TickRate);

        await server.WaitAssertion(() =>
        {
            var engine = entMan.GetComponent<VehicleEngineComponent>(vehicle);
            engine.EngineOn = true;
            exitDelay = engine.ExitDelay;

            Assert.That(buckle.TryUnbuckle(driver, driver), Is.False);
            Assert.That(engine.Exiting, Is.EqualTo(driver));
            Assert.That(engine.EngineOn, Is.False);
        });

        await pair.RunTicksSync((int) (timing.TickRate * exitDelay.TotalSeconds * 0.5));
        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<BuckleComponent>(driver).Buckled, Is.True));

        await pair.RunTicksSync((int) (timing.TickRate * exitDelay.TotalSeconds * 0.5) + 5);
        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<BuckleComponent>(driver).Buckled, Is.False));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DriverSeatSwitchWaitsForEngineStop()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var seats = server.System<VehicleSeatsSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();

        EntityUid vehicle = default;
        EntityUid driver = default;
        TimeSpan exitDelay = default;

        await server.WaitPost(() =>
        {
            vehicle = entMan.SpawnEntity("LuaMVehicleHumvee", new MapCoordinates(Vector2.Zero, map.MapId));
            driver = Seat(entMan, buckle, vehicle, 0);
        });

        await pair.RunTicksSync(timing.TickRate);

        await server.WaitAssertion(() =>
        {
            var engine = entMan.GetComponent<VehicleEngineComponent>(vehicle);
            engine.EngineOn = true;
            exitDelay = engine.ExitDelay;

            seats.RequestSwitchSeat((vehicle, entMan.GetComponent<VehicleSeatsComponent>(vehicle)), driver, 1);
            Assert.That(entMan.GetComponent<VehicleOccupantComponent>(driver).Seat, Is.EqualTo(0));
            Assert.That(engine.Exiting, Is.EqualTo(driver));
            Assert.That(buckle.TryUnbuckle(driver, driver), Is.False);
        });

        await pair.RunTicksSync((int) (timing.TickRate * exitDelay.TotalSeconds) + 5);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<BuckleComponent>(driver).Buckled, Is.True);
            Assert.That(entMan.GetComponent<VehicleOccupantComponent>(driver).Seat, Is.EqualTo(1));
            Assert.That(entMan.GetComponent<VehicleSeatsComponent>(vehicle).Driver, Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("MobHuman")]
    [TestCase("MobVoidBaseTSFD")]
    public async Task RamKnocksDownAndThrowsMob(string proto)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var standing = server.System<StandingStateSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();

        EntityUid mob = default;
        var start = Vector2.Zero;

        await server.WaitPost(() =>
        {
            var gravity = entMan.EnsureComponent<GravityComponent>(map.Grid);
            gravity.Enabled = true;
            gravity.Inherent = true;
            entMan.Dirty(map.Grid, gravity);
            var mapSys = entMan.System<SharedMapSystem>();
            for (var x = -3; x <= 3; x++)
            {
                for (var y = -8; y <= 3; y++)
                {
                    mapSys.SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            var vehicle = entMan.SpawnEntity("LuaMVehicleHumvee", map.GridCoords);
            var forward = Angle.Zero.ToWorldVec();
            mob = entMan.SpawnEntity(proto, map.GridCoords.Offset(forward * 2.2f));
            start = xformSys.GetWorldPosition(mob);

            var mover = entMan.GetComponent<GridVehicleMoverComponent>(vehicle);
            mover.CurrentSpeed = 10f;
            mover.IsMoving = true;
        });

        await pair.RunTicksSync(timing.TickRate / 2);

        await server.WaitAssertion(() =>
        {
            if (entMan.HasComponent<LayingDownComponent>(mob))
                Assert.That(standing.IsDown(mob), Is.True);

            Assert.That((xformSys.GetWorldPosition(mob) - start).Length(), Is.GreaterThan(1.5f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TankCannonSwitchesLoadsAndFiresShells()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var hardpoints = server.System<HardpointSystem>();
        var shells = server.System<VehicleShellSystem>();
        var gunSys = server.System<SharedGunSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();

        EntityUid vehicle = default;
        EntityUid gunner = default;
        EntityUid cannon = default;

        await server.WaitPost(() =>
        {
            vehicle = entMan.SpawnEntity("LuaMVehicleTank", new MapCoordinates(Vector2.Zero, map.MapId));
            gunner = Seat(entMan, buckle, vehicle, 1);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(hardpoints.TryGetWeapon(vehicle, HardpointType.Cannon, out var weapon, out _), Is.True);
            cannon = weapon!.Value;
            var mag = entMan.GetComponent<VehicleShellMagazineComponent>(cannon);
            Assert.That(mag.Counts, Is.EqualTo(new[] { 6, 4, 4 }));

            var ev = new VehicleSwitchShellActionEvent { Performer = gunner };
            entMan.EventBus.RaiseLocalEvent(vehicle, ev);
            Assert.That(mag.Selected, Is.EqualTo(1));

            Assert.That(shells.TryFindShellTarget(vehicle, "TankShellHEAT", out var target, out var index), Is.True);
            Assert.That(index, Is.EqualTo(2));
            Assert.That(shells.AddShells(target, index, 100), Is.EqualTo(8));
        });

        await pair.RunTicksSync(timing.TickRate * 5);

        await server.WaitAssertion(() =>
        {
            var gun = entMan.GetComponent<GunComponent>(cannon);
            gunSys.AttemptShoot(gunner, cannon, gun, new EntityCoordinates(map.MapUid, new Vector2(0f, -10f)));
            var mag = entMan.GetComponent<VehicleShellMagazineComponent>(cannon);
            Assert.That(mag.Counts, Is.EqualTo(new[] { 6, 3, 12 }));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MisalignedTurretKeepsReload()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var hardpoints = server.System<HardpointSystem>();
        var gunSys = server.System<SharedGunSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();

        EntityUid vehicle = default;
        EntityUid gunner = default;

        await server.WaitPost(() =>
        {
            vehicle = entMan.SpawnEntity("LuaMVehicleTank", new MapCoordinates(Vector2.Zero, map.MapId));
            gunner = Seat(entMan, buckle, vehicle, 1);
        });

        await pair.RunTicksSync(timing.TickRate * 5);

        await server.WaitAssertion(() =>
        {
            Assert.That(hardpoints.TryGetWeapon(vehicle, HardpointType.Cannon, out var cannon, out var gun), Is.True);
            var turret = entMan.GetComponent<VehicleTurretComponent>(vehicle);
            turret.Rotation = Angle.Zero;
            var mag = entMan.GetComponent<VehicleShellMagazineComponent>(cannon!.Value);
            var before = mag.Counts[mag.Selected];
            var sideways = new EntityCoordinates(map.MapUid, new Vector2(10f, 0f));
            var behind = new EntityCoordinates(map.MapUid, new Vector2(0f, 10f));

            gunSys.AttemptShoot(gunner, cannon.Value, gun!, sideways);
            gunSys.AttemptShoot(gunner, cannon.Value, gun!, behind);

            Assert.That(mag.Counts[mag.Selected], Is.EqualTo(before));
            Assert.That(gun!.NextFire, Is.LessThanOrEqualTo(timing.CurTime));
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("LuaMVehicleTank")]
    [TestCase("LuaMVehicleSPPVan")]
    public async Task ExplosionThrowDoesNotMoveVehicle(string proto)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var throwing = server.System<ThrowingSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var vehicle = entMan.SpawnEntity(proto, new MapCoordinates(Vector2.Zero, map.MapId));
            throwing.TryThrow(vehicle, new Vector2(5f, 0f), 20f);
            Assert.That(entMan.GetComponent<PhysicsComponent>(vehicle).LinearVelocity.Length(), Is.LessThan(0.01f));
            entMan.DeleteEntity(vehicle);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FuelBurnsAndEngineStallsWhenEmpty()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var fuelSys = server.System<VehicleFuelSystem>();
        var solutions = server.System<SharedSolutionContainerSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();

        EntityUid vehicle = default;
        await server.WaitPost(() =>
        {
            vehicle = entMan.SpawnEntity("LuaMVehicleHumvee", new MapCoordinates(Vector2.Zero, map.MapId));
            var fuel = entMan.GetComponent<VehicleFuelComponent>(vehicle);
            fuel.ConsumptionPerMinute = 600f;
            entMan.GetComponent<VehicleEngineComponent>(vehicle).EngineOn = true;
        });

        await pair.RunTicksSync(timing.TickRate * 4);

        await server.WaitAssertion(() =>
        {
            var fuel = entMan.GetComponent<VehicleFuelComponent>(vehicle);
            Assert.That(fuelSys.GetFuel(vehicle, fuel), Is.LessThan(FixedPoint2.New(250)));
            Assert.That(solutions.TryGetSolution(vehicle, fuel.Solution, out var soln, out _), Is.True);
            solutions.RemoveAllSolution(soln!.Value);
        });

        await pair.RunTicksSync(timing.TickRate * 4);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<VehicleEngineComponent>(vehicle).EngineOn, Is.False);
            var canMove = new VehicleCanMoveEvent();
            entMan.EventBus.RaiseLocalEvent(vehicle, ref canMove);
            Assert.That(canMove.Cancelled, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FuelTankRejectsOtherReagents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var vehicle = entMan.SpawnEntity("LuaMVehicleHumvee", new MapCoordinates(Vector2.Zero, map.MapId));
            var fuelCan = entMan.SpawnEntity("JerryCanWeldingFuel", new MapCoordinates(Vector2.Zero, map.MapId));
            var waterCan = entMan.SpawnEntity("JerryCanWeldingFuel", new MapCoordinates(Vector2.Zero, map.MapId));
            var solutions = entMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(waterCan, "tank", out var tank, out _), Is.True);
            solutions.RemoveAllSolution(tank!.Value);
            solutions.TryAddReagent(tank.Value, "Water", FixedPoint2.New(50), out _);

            var ok = new SolutionTransferAttemptEvent(fuelCan, vehicle);
            entMan.EventBus.RaiseLocalEvent(vehicle, ref ok);
            Assert.That(ok.CancelReason, Is.Null);

            var bad = new SolutionTransferAttemptEvent(waterCan, vehicle);
            entMan.EventBus.RaiseLocalEvent(vehicle, ref bad);
            Assert.That(bad.CancelReason, Is.Not.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FlechettePiercesMobsAndStopsAtWalls()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var projectiles = server.System<Content.Server.Projectiles.ProjectileSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var pos = new MapCoordinates(new Vector2(3f, 0f), map.MapId);
            var first = entMan.SpawnEntity("MobHuman", pos);
            var second = entMan.SpawnEntity("MobHuman", pos);
            var wall = entMan.SpawnEntity("WallSolid", pos);

            var shard = entMan.SpawnEntity("LuaMShrapnelTankFlechette", pos);
            var proj = entMan.GetComponent<ProjectileComponent>(shard);
            var body = entMan.GetComponent<PhysicsComponent>(shard);

            projectiles.ProjectileCollide((shard, proj, body), first);
            Assert.That(proj.ProjectileSpent, Is.False);

            projectiles.ProjectileCollide((shard, proj, body), second);
            Assert.That(proj.ProjectileSpent, Is.False);
            Assert.That(entMan.GetComponent<DamageableComponent>(second).TotalDamage, Is.GreaterThan(FixedPoint2.Zero));

            projectiles.ProjectileCollide((shard, proj, body), wall);
            Assert.That(proj.ProjectileSpent, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("VehicleLuaMMotorcycle", 1)]
    [TestCase("VehicleLuaMATV", 2)]
    public async Task BikeCrashThrowsRidersOff(string proto, int seats)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();

        var riders = new List<EntityUid>();
        await server.WaitPost(() =>
        {
            var gravity = entMan.EnsureComponent<GravityComponent>(map.Grid);
            gravity.Enabled = true;
            gravity.Inherent = true;
            entMan.Dirty(map.Grid, gravity);
            var mapSys = entMan.System<SharedMapSystem>();
            for (var x = -3; x <= 3; x++)
            {
                for (var y = -8; y <= 3; y++)
                {
                    mapSys.SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            var bike = entMan.SpawnEntity(proto, map.GridCoords);
            Assert.That(entMan.GetComponent<VehicleSeatsComponent>(bike).Seats, Has.Count.EqualTo(seats));
            for (var i = 0; i < seats; i++)
            {
                riders.Add(Seat(entMan, buckle, bike, i));
            }

            entMan.SpawnEntity("WallSolid", map.GridCoords.Offset(Angle.Zero.ToWorldVec() * 2f));
            var mover = entMan.GetComponent<GridVehicleMoverComponent>(bike);
            mover.CurrentSpeed = 14f;
            mover.IsMoving = true;
        });

        await pair.RunTicksSync(timing.TickRate);

        await server.WaitAssertion(() =>
        {
            foreach (var rider in riders)
            {
                Assert.That(entMan.GetComponent<BuckleComponent>(rider).Buckled, Is.False);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VehicleKeepsMomentumWhenWeightless()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();

        EntityUid vehicle = default;
        await server.WaitPost(() =>
        {
            vehicle = entMan.SpawnEntity("LuaMVehicleHumvee", map.GridCoords);
            var mover = entMan.GetComponent<GridVehicleMoverComponent>(vehicle);
            mover.CurrentSpeed = 6f;
            mover.IsMoving = true;
        });

        await pair.RunTicksSync(timing.TickRate / 2);

        await server.WaitAssertion(() =>
        {
            var mover = entMan.GetComponent<GridVehicleMoverComponent>(vehicle);
            Assert.That(mover.Floating, Is.True);
            Assert.That(mover.CurrentSpeed, Is.EqualTo(0f));
            Assert.That(entMan.GetComponent<PhysicsComponent>(vehicle).LinearVelocity.Length(), Is.GreaterThan(3f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OnlyGhostsCanPullVehicles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var pos = new MapCoordinates(Vector2.Zero, map.MapId);
            var vehicle = entMan.SpawnEntity("LuaMVehicleTank", pos);
            var human = entMan.SpawnEntity("MobHuman", pos);
            var aghost = entMan.SpawnEntity("AdminObserver", pos);

            var byHuman = new BeingPulledAttemptEvent(human, vehicle);
            entMan.EventBus.RaiseLocalEvent(vehicle, byHuman);
            Assert.That(byHuman.Cancelled, Is.True);

            var byGhost = new BeingPulledAttemptEvent(aghost, vehicle);
            entMan.EventBus.RaiseLocalEvent(vehicle, byGhost);
            Assert.That(byGhost.Cancelled, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("VehicleHoverbikeMailcarrier")]
    [TestCase("VehicleHoverbikeNfsd")]
    [TestCase("VehicleHoverbikePirate")]
    [TestCase("VehicleHoverbikeSyndicate")]
    [TestCase("VehicleLuaMHoverbike")]
    public async Task HoverbikesUseNewVehicleSystem(string proto)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var bike = entMan.SpawnEntity(proto, new MapCoordinates(Vector2.Zero, map.MapId));
            Assert.That(entMan.HasComponent<GridVehicleMoverComponent>(bike), Is.True);
            Assert.That(entMan.HasComponent<HoverVehicleComponent>(bike), Is.True);
            Assert.That(entMan.HasComponent<VehicleFuelComponent>(bike), Is.True);
            entMan.DeleteEntity(bike);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("LuaMVehicleHumveeOneMan")]
    [TestCase("LuaMVehicleTankOneMan")]
    [TestCase("LuaMVehicleSPPVanOneMan")]
    public async Task OneManDriverAlsoGuns(string proto)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var turrets = server.System<VehicleTurretSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var vehicle = entMan.SpawnEntity(proto, new MapCoordinates(Vector2.Zero, map.MapId));
            var seats = entMan.GetComponent<VehicleSeatsComponent>(vehicle);
            Assert.That(seats.Seats, Does.Not.Contain(VehicleSeatRole.Gunner));

            var driver = Seat(entMan, buckle, vehicle, 0);
            Assert.That(seats.Driver, Is.EqualTo(driver));
            Assert.That(seats.Gunner, Is.EqualTo(driver));

            var turret = entMan.GetComponent<VehicleTurretComponent>(vehicle);
            var gun = new LuaMGetGunEvent();
            entMan.EventBus.RaiseLocalEvent(driver, ref gun);
            if (turrets.TryGetSelectedWeapon(vehicle, turret, out var weapon, out _))
                Assert.That(gun.Gun, Is.EqualTo(weapon));
            else
                Assert.That(gun.Gun, Is.Null);

            buckle.Unbuckle(driver, null);
            Assert.That(seats.Gunner, Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TankCannonHeldFireDoesNotSkipReload()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var hardpoints = server.System<HardpointSystem>();
        var gunSys = server.System<SharedGunSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();

        EntityUid gunner = default;
        EntityUid cannon = default;
        var target = new EntityCoordinates(map.MapUid, new Vector2(0f, -10f));

        await server.WaitPost(() =>
        {
            var vehicle = entMan.SpawnEntity("LuaMVehicleTank", new MapCoordinates(Vector2.Zero, map.MapId));
            gunner = Seat(entMan, buckle, vehicle, 1);
            Assert.That(hardpoints.TryGetWeapon(vehicle, HardpointType.Cannon, out var weapon, out _), Is.True);
            cannon = weapon!.Value;
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
            gunSys.AttemptShoot(gunner, cannon, entMan.GetComponent<GunComponent>(cannon), target));

        await pair.RunTicksSync(timing.TickRate * 4 + 5);

        await server.WaitAssertion(() =>
        {
            var gun = entMan.GetComponent<GunComponent>(cannon);
            gun.ShotCounter = 1;
            gunSys.AttemptShoot(gunner, cannon, gun, target);
            var mag = entMan.GetComponent<VehicleShellMagazineComponent>(cannon);
            Assert.That(mag.Counts[mag.Selected], Is.EqualTo(4), "a held trigger fires as soon as the shell is loaded");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AtvUsesNewVehicleSystem()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var atv = entMan.SpawnEntity("VehicleLuaMATV", new MapCoordinates(Vector2.Zero, map.MapId));
            Assert.That(entMan.HasComponent<GridVehicleMoverComponent>(atv), Is.True);
            Assert.That(entMan.HasComponent<HoverVehicleComponent>(atv), Is.False);
            Assert.That(entMan.GetComponent<VehicleFuelComponent>(atv).Enabled, Is.True);
            Assert.That(entMan.System<ItemSlotsSystem>().TryGetSlot(atv, "bag", out _), Is.True);
            entMan.DeleteEntity(atv);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task JanicartKeepsCleaningKitWithoutFuel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var coords = new MapCoordinates(Vector2.Zero, map.MapId);
            var cart = entMan.SpawnEntity("VehicleLuaMJanicart", coords);
            var mop = entMan.SpawnEntity("MopItem", coords);
            var bag = entMan.SpawnEntity("TrashBag", coords);
            var waterCan = entMan.SpawnEntity("JerryCanWeldingFuel", coords);
            var driver = entMan.SpawnEntity("MobHuman", coords);
            var slots = entMan.System<ItemSlotsSystem>();
            var solutions = entMan.System<SharedSolutionContainerSystem>();

            Assert.That(entMan.HasComponent<GridVehicleMoverComponent>(cart), Is.True);
            Assert.That(slots.TryInsert(cart, "mop_slot", mop, null), Is.True);
            Assert.That(slots.TryInsert(cart, "trashbag_slot", bag, null), Is.True);

            var start = new VehicleEngineStartAttemptEvent(driver);
            entMan.EventBus.RaiseLocalEvent(cart, ref start);
            Assert.That(start.Cancelled, Is.False);

            Assert.That(solutions.TryGetSolution(waterCan, "tank", out var tank, out _), Is.True);
            solutions.RemoveAllSolution(tank!.Value);
            solutions.TryAddReagent(tank.Value, "Water", FixedPoint2.New(50), out _);
            var pour = new SolutionTransferAttemptEvent(waterCan, cart);
            entMan.EventBus.RaiseLocalEvent(cart, ref pour);
            Assert.That(pour.CancelReason, Is.Null);

            Assert.That(solutions.TryGetRefillableSolution(cart, out _, out var bucket), Is.True);
            Assert.That(bucket!.MaxVolume, Is.EqualTo(FixedPoint2.New(800)));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LyingMobCannotTakeSeat()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var standing = server.System<StandingStateSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var vehicle = entMan.SpawnEntity("LuaMVehicleHumvee", new MapCoordinates(Vector2.Zero, map.MapId));
            var mob = entMan.SpawnEntity("MobHuman", entMan.GetComponent<TransformComponent>(vehicle).Coordinates);
            Assert.That(standing.Down(mob), Is.True);
            Assert.That(buckle.TryBuckle(mob, mob, vehicle), Is.False);
            Assert.That(entMan.GetComponent<BuckleComponent>(mob).Buckled, Is.False);

            Assert.That(standing.Stand(mob), Is.True);
            Assert.That(buckle.TryBuckle(mob, mob, vehicle), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("LuaMBulletTankHEAT", "LuaMPelletTankHEAT")]
    [TestCase("LuaMBulletTankFlechette", "LuaMShrapnelTankFlechette")]
    public async Task ShellFragmentsDoNotPointAtTheDeletedShell(string shellProto, string fragmentProto)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var trigger = server.System<Content.Server.Explosion.EntitySystems.TriggerSystem>();
        var map = await pair.CreateTestMap();

        EntityUid gunner = default;
        EntityUid shell = default;
        await server.WaitPost(() =>
        {
            gunner = entMan.SpawnEntity("MobHuman", new MapCoordinates(new Vector2(-5f, 0f), map.MapId));
            shell = entMan.SpawnEntity(shellProto, new MapCoordinates(Vector2.Zero, map.MapId));
            var projectile = entMan.GetComponent<ProjectileComponent>(shell);
            projectile.Shooter = gunner;
            projectile.Weapon = gunner;
            trigger.Trigger(shell);
            entMan.QueueDeleteEntity(shell);
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(shell), Is.True);
            var fragments = 0;
            var query = entMan.EntityQueryEnumerator<ProjectileComponent, MetaDataComponent>();
            while (query.MoveNext(out _, out var projectile, out var meta))
            {
                if (meta.EntityPrototype?.ID != fragmentProto)
                    continue;

                fragments++;
                Assert.That(projectile.Shooter, Is.EqualTo(gunner));
                Assert.That(projectile.Weapon, Is.EqualTo(gunner));
            }

            Assert.That(fragments, Is.GreaterThan(0));
        });

        await pair.CleanReturnAsync();
    }

    private static readonly Vector2i[] JamWalls =
    {
        new(-1, -1),
        new(0, -2), new(1, -2),
        new(-2, 0), new(-2, 1),
    };

    // -- Хамви по диагонали залез серединой борта в угол стены, спереди и сзади тоже стены: раньше он вставал намертво
    [TestCase(MoveButtons.Up | MoveButtons.Left, MoveButtons.Down | MoveButtons.Right)]
    [TestCase(MoveButtons.Down | MoveButtons.Left, MoveButtons.Up | MoveButtons.Right)]
    public async Task HumveeWedgedOnWallCornerCanDriveOut(MoveButtons first, MoveButtons second)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var xforms = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();

        var startPos = new Vector2(0.438f, 0.438f);
        var startRot = Angle.FromDegrees(225);
        EntityUid humvee = default;
        EntityUid driver = default;

        await server.WaitPost(() =>
        {
            PrepareFloor(entMan, map);
            humvee = entMan.SpawnEntity("LuaMVehicleHumvee", new EntityCoordinates(map.Grid, Vector2.Zero));
            driver = Seat(entMan, buckle, humvee, 0);

            foreach (var tile in JamWalls)
            {
                entMan.SpawnEntity("WallSolid", new EntityCoordinates(map.Grid, new Vector2(tile.X + 0.5f, tile.Y + 0.5f)));
            }

            xforms.SetLocalPositionRotation(humvee, startPos, startRot);
            StartEngine(entMan, timing, humvee);
            Assert.That(OverlapsWalls(startPos, startRot), Is.True, "the humvee starts wedged into the wall corner");
        });

        await pair.RunTicksSync(2);
        await server.WaitPost(() => entMan.GetComponent<InputMoverComponent>(driver).HeldMoveButtons = first);
        await pair.RunTicksSync(timing.TickRate);

        var freedAt = Vector2.Zero;
        await server.WaitAssertion(() =>
        {
            var xform = entMan.GetComponent<TransformComponent>(humvee);
            Assert.That(OverlapsWalls(xform.LocalPosition, xform.LocalRotation), Is.False, "the humvee was pushed out of the wall");
            Assert.That((xform.LocalPosition - startPos).Length(), Is.LessThan(0.6f), "it was only nudged out, not thrown around");
            freedAt = xform.LocalPosition;
            entMan.GetComponent<InputMoverComponent>(driver).HeldMoveButtons = second;
        });

        await pair.RunTicksSync(timing.TickRate * 2);

        await server.WaitAssertion(() =>
        {
            var xform = entMan.GetComponent<TransformComponent>(humvee);
            var moved = (xform.LocalPosition - freedAt).Length();
            Assert.That(moved, Is.GreaterThan(0.1f), $"the humvee can drive again (moved {moved:0.00})");
            Assert.That(OverlapsWalls(xform.LocalPosition, xform.LocalRotation), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    // -- Ткнувшись носом в стену под углом, машина встаёт, а не уезжает вдоль неё
    [Test]
    public async Task HumveeStopsAtWallInsteadOfSliding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var xforms = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();

        var startRot = Angle.FromDegrees(20);
        EntityUid humvee = default;
        EntityUid driver = default;

        await server.WaitPost(() =>
        {
            PrepareFloor(entMan, map);
            humvee = entMan.SpawnEntity("LuaMVehicleHumvee", new EntityCoordinates(map.Grid, Vector2.Zero));
            driver = Seat(entMan, buckle, humvee, 0);
            for (var x = -12; x <= 12; x++)
            {
                entMan.SpawnEntity("WallSolid", new EntityCoordinates(map.Grid, new Vector2(x + 0.5f, -3.5f)));
            }

            xforms.SetLocalPositionRotation(humvee, Vector2.Zero, startRot);
            StartEngine(entMan, timing, humvee);
        });

        await pair.RunTicksSync(2);
        await server.WaitPost(() => entMan.GetComponent<InputMoverComponent>(driver).HeldMoveButtons = MoveButtons.Up);
        await pair.RunTicksSync(timing.TickRate * 3);

        await server.WaitAssertion(() =>
        {
            var xform = entMan.GetComponent<TransformComponent>(humvee);
            var mover = entMan.GetComponent<GridVehicleMoverComponent>(humvee);
            Assert.That(xform.LocalPosition.Y, Is.LessThan(-0.5f), "it drove up to the wall");
            Assert.That(MathF.Abs(xform.LocalPosition.X), Is.LessThan(1f), $"it did not slide along the wall (x {xform.LocalPosition.X:0.00})");
            Assert.That(mover.CurrentSpeed, Is.EqualTo(0f).Within(0.5f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RamCooldownSurvivesOtherVehicleRemoval()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        EntityUid humvee = default;
        EntityUid wall = default;
        var firstHit = FixedPoint2.Zero;

        await server.WaitPost(() =>
        {
            PrepareFloor(entMan, map);
            humvee = entMan.SpawnEntity("LuaMVehicleHumvee", new EntityCoordinates(map.Grid, Vector2.Zero));
            wall = entMan.SpawnEntity("WallSolid", new EntityCoordinates(map.Grid, new Vector2(0.5f, -2.5f)));
            entMan.GetComponent<GridVehicleMoverComponent>(humvee).CurrentSpeed = 10f;
        });

        await pair.RunTicksSync(12);

        await server.WaitPost(() =>
        {
            firstHit = entMan.GetComponent<DamageableComponent>(wall).TotalDamage;
            Assert.That(firstHit, Is.GreaterThan(FixedPoint2.Zero));

            var other = entMan.SpawnEntity("LuaMVehicleHumvee", new EntityCoordinates(map.Grid, new Vector2(20f, 0f)));
            entMan.DeleteEntity(other);
            entMan.GetComponent<GridVehicleMoverComponent>(humvee).CurrentSpeed = 10f;
        });

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<DamageableComponent>(wall).TotalDamage, Is.EqualTo(firstHit));
            entMan.DeleteEntity(humvee);
        });

        await pair.CleanReturnAsync();
    }

    private static void PrepareFloor(IEntityManager entMan, TestMapData map)
    {
        var gravity = entMan.EnsureComponent<GravityComponent>(map.Grid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        entMan.Dirty(map.Grid, gravity);
        var mapSys = entMan.System<SharedMapSystem>();
        for (var x = -30; x <= 30; x++)
        {
            for (var y = -30; y <= 30; y++)
            {
                mapSys.SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
            }
        }
    }

    private static void StartEngine(IEntityManager entMan, IGameTiming timing, EntityUid vehicle)
    {
        var engine = entMan.GetComponent<VehicleEngineComponent>(vehicle);
        engine.EngineOn = true;
        engine.ReadyAt = timing.CurTime;
    }

    private static bool OverlapsWalls(Vector2 pos, Angle rot)
    {
        var hull = new Vector2[4];
        var corners = new[] { new Vector2(-0.65f, -1.2f), new Vector2(0.65f, -1.2f), new Vector2(0.65f, 1.2f), new Vector2(-0.65f, 1.2f) };
        for (var i = 0; i < 4; i++)
        {
            hull[i] = pos + rot.RotateVec(corners[i]);
        }

        foreach (var tile in JamWalls)
        {
            var wall = new[] { new Vector2(tile.X, tile.Y), new Vector2(tile.X + 1, tile.Y), new Vector2(tile.X + 1, tile.Y + 1), new Vector2(tile.X, tile.Y + 1) };
            if (Separation(hull, wall) < -0.005f)
                return true;
        }

        return false;
    }

    private static float Separation(Vector2[] a, Vector2[] b)
    {
        var best = float.MinValue;
        foreach (var poly in new[] { a, b })
        {
            for (var i = 0; i < poly.Length; i++)
            {
                var edge = poly[(i + 1) % poly.Length] - poly[i];
                var axis = Vector2.Normalize(new Vector2(edge.Y, -edge.X));
                float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue;
                foreach (var p in a)
                {
                    var d = Vector2.Dot(p, axis);
                    minA = Math.Min(minA, d);
                    maxA = Math.Max(maxA, d);
                }

                foreach (var p in b)
                {
                    var d = Vector2.Dot(p, axis);
                    minB = Math.Min(minB, d);
                    maxB = Math.Max(maxB, d);
                }

                best = Math.Max(best, Math.Max(minB - maxA, minA - maxB));
            }
        }

        return best;
    }

    private static EntityUid Seat(IEntityManager entMan, SharedBuckleSystem buckle, EntityUid vehicle, int seat)
    {
        var mob = entMan.SpawnEntity("MobHuman", entMan.GetComponent<TransformComponent>(vehicle).Coordinates);
        var slots = entMan.GetComponent<StrapSlotsComponent>(vehicle);
        slots.Requested[mob] = seat;
        Assert.That(buckle.TryBuckle(mob, mob, vehicle), Is.True);
        slots.Requested.Remove(mob);
        Assert.That(entMan.GetComponent<VehicleOccupantComponent>(mob).Seat, Is.EqualTo(seat));
        return mob;
    }
}

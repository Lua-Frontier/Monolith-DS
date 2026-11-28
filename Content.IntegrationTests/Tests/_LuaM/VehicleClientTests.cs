#nullable enable
using System.Linq;
using System.Numerics;
using Content.Client._LuaM.Vehicles.Dashboard;
using Content.Client.UserInterface.Systems.Alerts.Controls;
using Content.Client.UserInterface.Systems.Alerts.Widgets;
using Content.Shared._LuaM.Buckle;
using Content.Shared._LuaM.Vehicles;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Buckle;
using Content.Shared.Mind;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class VehicleClientTests
{
    [Test]
    public async Task HudFollowsSeatAndBuckleAlertShows()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, DummyTicker = false });
        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.ResolveDependency<IEntityManager>();
        var clientEntMan = client.ResolveDependency<IEntityManager>();
        var clientUi = client.ResolveDependency<IUserInterfaceManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var seats = server.System<VehicleSeatsSystem>();

        var player = await PlaceRiderOnTestGrid(pair);
        EntityUid tank = default;
        await server.WaitAssertion(() =>
        {
            tank = entMan.SpawnEntity("LuaMVehicleTank", entMan.GetComponent<TransformComponent>(player).Coordinates);
            var slots = entMan.GetComponent<StrapSlotsComponent>(tank);
            slots.Requested[player] = 0;
            Assert.That(buckle.TryBuckle(player, player, tank), Is.True);
            slots.Requested.Remove(player);
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var driver = Find<VehicleDashboard>(clientUi.ActiveScreen!);
            var gunner = Find<VehicleGunnerPanel>(clientUi.ActiveScreen!);
            Assert.That(driver, Is.Not.Null);
            Assert.That(gunner, Is.Not.Null);
            Assert.That(driver!.Visible, Is.True);
            Assert.That(gunner!.Visible, Is.False);
            Assert.That(driver.Title, Is.Not.Empty);

            var alerts = Find<AlertsUI>(clientUi.ActiveScreen!);
            Assert.That(alerts, Is.Not.Null);
            var ids = alerts!.AlertContainer.Children.OfType<AlertControl>().Select(c => c.Alert.ID).ToArray();
            Assert.That(ids, Does.Contain("Buckled"));
        });

        await server.WaitAssertion(() =>
        {
            seats.SwitchSeat((tank, entMan.GetComponent<VehicleSeatsComponent>(tank)), player, 1);
            Assert.That(entMan.GetComponent<VehicleOccupantComponent>(player).Role, Is.EqualTo(VehicleSeatRole.Gunner));
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var driver = Find<VehicleDashboard>(clientUi.ActiveScreen!);
            var gunner = Find<VehicleGunnerPanel>(clientUi.ActiveScreen!);
            Assert.That(driver!.Visible, Is.False);
            Assert.That(gunner!.Visible, Is.True);
            Assert.That(gunner.Shells, Has.Count.EqualTo(3));
            Assert.That(gunner.Ammo, Is.GreaterThan(0));
        });

        await server.WaitAssertion(() => buckle.Unbuckle(player, player));
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            Assert.That(Find<VehicleDashboard>(clientUi.ActiveScreen!)!.Visible, Is.False);
            Assert.That(Find<VehicleGunnerPanel>(clientUi.ActiveScreen!)!.Visible, Is.False);
        });

        await server.WaitPost(() => entMan.DeleteEntity(tank));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MotorcycleRiderSitsOnTopWithDirectionalLayers()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, DummyTicker = false });
        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.ResolveDependency<IEntityManager>();
        var clientEntMan = client.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();
        var xforms = server.System<SharedTransformSystem>();

        EntityUid player = default;
        EntityUid bike = default;
        var baseline = Vector2.Zero;
        player = await VehicleClientTests.PlaceRiderOnTestGrid(pair);
        await pair.RunTicksSync(30);
        var netPlayerEarly = entMan.GetNetEntity(player);
        await client.WaitPost(() => baseline = clientEntMan.GetComponent<SpriteComponent>(clientEntMan.GetEntity(netPlayerEarly)).Offset);

        await server.WaitAssertion(() =>
        {
            bike = entMan.SpawnEntity("VehicleLuaMMotorcycle", entMan.GetComponent<TransformComponent>(player).Coordinates);
            xforms.SetLocalRotation(bike, Direction.East.ToAngle());
            var slots = entMan.GetComponent<StrapSlotsComponent>(bike);
            slots.Requested[player] = 0;
            Assert.That(buckle.TryBuckle(player, player, bike), Is.True);
            slots.Requested.Remove(player);
        });

        await pair.RunTicksSync(10);

        var netBike = entMan.GetNetEntity(bike);
        var netPlayer = entMan.GetNetEntity(player);
        await client.WaitAssertion(() =>
        {
            var bikeSprite = clientEntMan.GetComponent<SpriteComponent>(clientEntMan.GetEntity(netBike));
            var riderSprite = clientEntMan.GetComponent<SpriteComponent>(clientEntMan.GetEntity(netPlayer));
            var riderDepth = riderSprite.DrawDepth;
            Assert.That(bikeSprite.DrawDepth, Is.LessThan(riderDepth), "side view: rider is drawn over the bike");
            Assert.That(riderSprite.Offset.Y - baseline.Y, Is.GreaterThan(0.1f), "rider sits on the saddle, not inside the bike");
        });

        await server.WaitPost(() => xforms.SetLocalRotation(bike, Direction.South.ToAngle()));
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var bikeSprite = clientEntMan.GetComponent<SpriteComponent>(clientEntMan.GetEntity(netBike));
            var riderSprite = clientEntMan.GetComponent<SpriteComponent>(clientEntMan.GetEntity(netPlayer));
            Assert.That(bikeSprite.DrawDepth, Is.GreaterThan(riderSprite.DrawDepth), "front view: the bike covers the rider");
            Assert.That(riderSprite.Offset.Y - baseline.Y, Is.GreaterThan(0.1f));
        });

        await server.WaitAssertion(() => buckle.Unbuckle(player, player));
        await pair.RunTicksSync(server.ResolveDependency<IGameTiming>().TickRate * 4);

        await client.WaitAssertion(() =>
        {
            var riderSprite = clientEntMan.GetComponent<SpriteComponent>(clientEntMan.GetEntity(netPlayer));
            var rider = clientEntMan.GetEntity(netPlayer);
            var z = clientEntMan.TryGetComponent<Content.Shared._CE.ZLevels.Core.Components.CEZPhysicsComponent>(rider, out var zPhys)
                ? $"z {zPhys.LocalPosition}, default {zPhys.SpriteOffsetDefault}"
                : "no z";
            Assert.That(riderSprite.Offset.Y, Is.LessThan(0.08f), $"seat shift is gone after getting off (now {riderSprite.Offset}, {z})");
        });

        await server.WaitPost(() => entMan.DeleteEntity(bike));
        await pair.CleanReturnAsync();
    }

    [TestCase("VehicleLuaMMotorcycle")]
    [TestCase("VehicleLuaMHoverbike")]
    [TestCase("VehicleLuaMATV")]
    [TestCase("VehicleLuaMJanicart")]
    public async Task SmallVehicleSpritesKeepCardinalFrames(string proto)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var entMan = client.ResolveDependency<IEntityManager>();

        await client.WaitAssertion(() =>
        {
            var sprites = entMan.System<SpriteSystem>();
            var vehicle = entMan.SpawnEntity(proto, MapCoordinates.Nullspace);
            var sprite = entMan.GetComponent<SpriteComponent>(vehicle);
            Assert.DoesNotThrow(() => sprites.GetLocalBounds((vehicle, sprite)));
            Assert.That(sprite.NoRotation, Is.True);
            Assert.That(sprite.SnapCardinals, Is.False);
            entMan.DeleteEntity(vehicle);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HoverVehicleIsPredictedForLocalDriver()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, DummyTicker = false });
        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.ResolveDependency<IEntityManager>();
        var clientEntMan = client.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();

        var player = await PlaceRiderOnTestGrid(pair);
        EntityUid bike = default;
        NetEntity netBike = default;
        await server.WaitAssertion(() =>
        {
            bike = entMan.SpawnEntity("VehicleLuaMHoverbike", entMan.GetComponent<TransformComponent>(player).Coordinates);
            netBike = entMan.GetNetEntity(bike);
            var slots = entMan.GetComponent<StrapSlotsComponent>(bike);
            slots.Requested[player] = 0;
            Assert.That(buckle.TryBuckle(player, player, bike), Is.True);
            slots.Requested.Remove(player);
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientBike = clientEntMan.GetEntity(netBike);
            Assert.That(clientEntMan.GetComponent<PhysicsComponent>(clientBike).Predict, Is.True);
        });

        await server.WaitAssertion(() => buckle.Unbuckle(player, player));
        await pair.RunTicksSync(server.ResolveDependency<IGameTiming>().TickRate * 3);

        await client.WaitAssertion(() =>
        {
            var clientBike = clientEntMan.GetEntity(netBike);
            Assert.That(clientEntMan.GetComponent<VehicleSeatsComponent>(clientBike).Driver, Is.Null);
            Assert.That(clientEntMan.GetComponent<PhysicsComponent>(clientBike).Predict, Is.False);
        });

        await server.WaitPost(() => entMan.DeleteEntity(bike));
        await pair.CleanReturnAsync();
    }

    internal static async Task<EntityUid> PlaceRiderOnTestGrid(Content.IntegrationTests.Pair.TestPair pair)
    {
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var maps = server.System<SharedMapSystem>();
        var minds = server.System<SharedMindSystem>();
        EntityUid rider = default;
        await server.WaitPost(() =>
        {
            for (var x = -3; x <= 3; x++)
            for (var y = -3; y <= 3; y++)
                maps.SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);

            var gravity = entities.EnsureComponent<Content.Shared.Gravity.GravityComponent>(map.Grid);
            gravity.Enabled = true;
            gravity.Inherent = true;
            entities.Dirty(map.Grid, gravity);

            // -- Сущность игрока в пуле может остаться призраком или с камерой, повёрнутой под прежний грид
            var session = server.ResolveDependency<IPlayerManager>().Sessions.Single();
            rider = entities.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            Assert.That(minds.TryGetMind(session.UserId, out var mindId, out var mind), Is.True);
            minds.TransferTo(mindId!.Value, rider, mind: mind);
        });
        return rider;
    }

    private static T? Find<T>(Control control) where T : Control
    {
        if (control is T found)
            return found;

        foreach (var child in control.Children)
        {
            if (Find<T>(child) is { } result)
                return result;
        }

        return null;
    }
}

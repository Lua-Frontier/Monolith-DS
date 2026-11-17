using System.Linq;
using System.Numerics;
using Content.Client._LuaM.StateRecovery;
using Content.Shared._LuaM.Buckle;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Robust.Client.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class RiderProbeTest
{
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public async Task RiderProbe(int run)
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
        player = await VehicleClientTests.PlaceRiderOnTestGrid(pair);
        await pair.RunTicksSync(30);

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
        await server.WaitPost(() => xforms.SetLocalRotation(bike, Direction.South.ToAngle()));
        await pair.RunTicksSync(10);

        var netPlayer = entMan.GetNetEntity(player);
        var netBike = entMan.GetNetEntity(bike);
        await client.WaitPost(() => Dump("client before", clientEntMan, clientEntMan.GetEntity(netPlayer), clientEntMan.GetEntity(netBike), client.System<StateRecoverySystem>().Requests));

        await server.WaitAssertion(() =>
        {
            buckle.Unbuckle(player, player);
            Dump("server after", entMan, player, bike, -1);
        });

        for (var i = 0; i < 4; i++)
        {
            await pair.RunTicksSync(30);
            await client.WaitPost(() => Dump($"client +{i + 1}s", clientEntMan, clientEntMan.GetEntity(netPlayer), clientEntMan.GetEntity(netBike), client.System<StateRecoverySystem>().Requests));
        }

        await server.WaitPost(() => entMan.DeleteEntity(bike));
        await pair.CleanReturnAsync();
    }

    private static void Dump(string tag, IEntityManager entMan, EntityUid player, EntityUid bike, int requests)
    {
        var buckled = entMan.TryGetComponent<BuckleComponent>(player, out var b) ? $"{b.Buckled} to {b.BuckledTo}" : "none";
        var strap = entMan.TryGetComponent<StrapComponent>(bike, out var s) ? string.Join(",", s.BuckledEntities) : "none";
        var occupant = entMan.HasComponent<VehicleOccupantComponent>(player);
        var offset = requests >= 0 && entMan.TryGetComponent<SpriteComponent>(player, out var sprite) ? sprite.Offset.ToString() : "-";
        var parent = entMan.GetComponent<TransformComponent>(player).ParentUid;
        var weightless = entMan.System<Content.Shared.Gravity.SharedGravitySystem>().IsWeightless(player);
        var canFloat = entMan.TryGetComponent<Content.Shared.Gravity.FloatingVisualsComponent>(player, out var fv) ? (fv.CanFloat ? "True" : "False") : "-";
        var grid = entMan.GetComponent<TransformComponent>(player).GridUid;
        var gravity = grid is { } g && entMan.TryGetComponent<Content.Shared.Gravity.GravityComponent>(g, out var gc) ? $"{gc.Enabled}/{gc.Inherent}" : "nogravcomp";
        var xf = entMan.System<SharedTransformSystem>();
        TestContext.Out.WriteLine($"   pos player {xf.GetWorldPosition(player)} bike {xf.GetWorldPosition(bike)} bikeParent {entMan.GetComponent<TransformComponent>(bike).ParentUid}");
        TestContext.Out.WriteLine($"   weightless {weightless}, canFloat {canFloat}, grid {grid} gravity {gravity}");
        TestContext.Out.WriteLine($"{tag}: buckle {buckled}, strap [{strap}], occupant {occupant}, offset {offset}, parent {parent}, fullState {requests}");
    }
}

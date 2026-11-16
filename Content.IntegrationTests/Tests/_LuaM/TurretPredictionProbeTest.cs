using System.Linq;
using System.Numerics;
using Content.Shared._LuaM.Buckle;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Buckle;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class TurretPredictionProbeTest
{
    [Test]
    public async Task LocalGunnerTurretIsPredicted()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, DummyTicker = false });
        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.ResolveDependency<IEntityManager>();
        var clientEntMan = client.ResolveDependency<IEntityManager>();
        var buckle = server.System<SharedBuckleSystem>();

        EntityUid player = default;
        EntityUid van = default;
        await server.WaitAssertion(() =>
        {
            player = server.ResolveDependency<IPlayerManager>().Sessions.Single().AttachedEntity!.Value;
            van = entMan.SpawnEntity("LuaMVehicleSPPVanOneMan", entMan.GetComponent<TransformComponent>(player).Coordinates);
            var slots = entMan.GetComponent<StrapSlotsComponent>(van);
            slots.Requested[player] = 0;
            Assert.That(buckle.TryBuckle(player, player, van), Is.True);
            slots.Requested.Remove(player);
            Assert.That(entMan.GetComponent<VehicleSeatsComponent>(van).Gunner, Is.EqualTo(player));
        });

        await pair.RunTicksSync(10);

        var clientVan = pair.ToClientUid(van);
        await client.WaitPost(() =>
        {
            clientEntMan.GetComponent<VehicleTurretComponent>(clientVan).AimOffset = new Vector2(0f, 6f);
        });

        await pair.RunTicksSync(5);

        Angle serverRot = default;
        await server.WaitAssertion(() => serverRot = entMan.GetComponent<VehicleTurretComponent>(van).Rotation);
        await client.WaitAssertion(() =>
        {
            var clientRot = clientEntMan.GetComponent<VehicleTurretComponent>(clientVan).Rotation;
            TestContext.Out.WriteLine($"server {serverRot.Degrees:F1}, client {clientRot.Degrees:F1}");
            Assert.That(Math.Abs(Angle.ShortestDistance(serverRot, clientRot).Degrees), Is.GreaterThan(1));
        });

        await server.WaitPost(() => entMan.DeleteEntity(van));
        await pair.CleanReturnAsync();
    }
}

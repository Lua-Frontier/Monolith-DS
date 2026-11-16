using Content.Server.Medical.SuitSensors;
using Content.Shared.Inventory;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class SuitSensorMindTests
{
    [Test]
    public async Task MindlessWearerIsNotReported()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var sensors = entities.System<SuitSensorSystem>();
            var inventory = entities.System<InventorySystem>();
            var minds = entities.System<SharedMindSystem>();

            var mob = entities.SpawnEntity("MobHuman", map.MapCoords);
            var uniform = entities.SpawnEntity("ClothingUniformJumpsuitColorGrey", map.MapCoords);
            Assert.That(inventory.TryEquip(mob, uniform, "jumpsuit", silent: true, force: true));

            var sensor = entities.GetComponent<SuitSensorComponent>(uniform);
            sensors.SetSensor((uniform, sensor), SuitSensorMode.SensorVitals);
            Assert.That(sensor.User, Is.EqualTo(mob));
            Assert.That(sensors.GetSensorState(uniform), Is.Null);

            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, mob);
            Assert.That(sensors.GetSensorState(uniform), Is.Not.Null);

            minds.TransferTo(mind, null, createGhost: false);
            Assert.That(sensors.GetSensorState(uniform), Is.Null);
        });

        await pair.CleanReturnAsync();
    }
}

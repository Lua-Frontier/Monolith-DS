using System.Linq;
using Content.Shared._LuaM.Cyberware;
using Content.Shared._White.Overlays;
using Content.Shared.Actions;
using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._LuaM;

/// <summary>
/// The thermal eyes trait puts the implant into the head, and the body gets the thermal pulse action on its hotbar.
/// </summary>
[TestFixture]
public sealed class ThermalEyesTraitTest
{
    [Test]
    public async Task TraitGivesThermalAction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();

        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            human = entities.SpawnEntity("MobHuman", map.GridCoords);
            entities.AddComponent(human, new TraitOrganForEachPartComponent
            {
                Organ = "ThermalCyberneticEyesBlackMarket",
                PartType = BodyPartType.Head,
                Replace = true,
            });
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.TryGetComponent<ThermalVisionComponent>(human, out var thermal), "no ThermalVision on the body");
            Assert.That(thermal!.ToggleActionEntity, Is.Not.Null, "ThermalVision has no action entity");

            var actions = entities.System<SharedActionsSystem>().GetActions(human).Select(a => a.Id).ToList();
            Assert.That(actions, Does.Contain(thermal.ToggleActionEntity!.Value), "the action is not in the body's actions");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The same with a connected player, the way it happens in a round: the client has to see the action too.
    /// </summary>
    [Test]
    public async Task ClientSeesThermalAction()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();

        EntityUid human = default;
        var session = server.PlayerMan.GetSessionById(client.Session!.UserId);

        await server.WaitPost(() =>
        {
            human = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, human);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            entities.AddComponent(human, new TraitOrganForEachPartComponent
            {
                Organ = "ThermalCyberneticEyesBlackMarket",
                PartType = BodyPartType.Head,
                Replace = true,
            });
        });

        await pair.RunTicksSync(30);

        await client.WaitAssertion(() =>
        {
            var cEntities = client.EntMan;
            var cHuman = pair.ToClientUid(human);
            Assert.That(cEntities.TryGetComponent<ThermalVisionComponent>(cHuman, out var thermal), "no ThermalVision on the client body");

            var actions = cEntities.System<SharedActionsSystem>().GetActions(cHuman)
                .Select(a => cEntities.GetComponent<MetaDataComponent>(a.Id).EntityPrototype?.ID)
                .ToList();
            Assert.That(actions, Does.Contain("PulseThermalVision"), $"client actions: {string.Join(", ", actions)}");
        });

        await pair.CleanReturnAsync();
    }
}

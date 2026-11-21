using System.Collections.Generic;
using System.Linq;
using Content.Server._LuaM.Maps;
using Content.Shared._LuaM.Performance;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Players;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GradualMapDeletionTest
{
    [Test]
    public async Task MapIsDeletedOverSeveralTicks()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var entities = server.EntMan;
        var mapSystem = entities.System<SharedMapSystem>();
        var player = server.ResolveDependency<IPlayerManager>().Sessions.Single();
        var testMap = await pair.CreateTestMap();
        await pair.CreateTestMap();

        var map = testMap.MapUid;
        var children = new List<EntityUid>();
        EntityUid body = default;

        await server.WaitAssertion(() =>
        {
            server.CfgMan.SetCVar(PerformanceCVars.MapDeletionBudgetMs, 0.001f);
            for (var i = 0; i < 200; i++)
            {
                children.Add(entities.SpawnEntity(null, new MapCoordinates(i, 50, testMap.MapId)));
            }

            body = entities.SpawnEntity(null, testMap.GridCoords);
            entities.System<SharedMindSystem>().TransferTo(player.ContentData()!.Mind!.Value, body);

            entities.System<GradualMapDeletionSystem>().QueueDelete(map);
            Assert.That(mapSystem.IsPaused(map));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(map));
            Assert.That(children.Count(entities.EntityExists), Is.InRange(1, children.Count - 1));
            Assert.That(entities.EntityExists(body), Is.False);
            var ghost = player.AttachedEntity;
            Assert.That(entities.HasComponent<GhostComponent>(ghost));
            Assert.That(entities.GetComponent<TransformComponent>(ghost!.Value).MapID, Is.Not.EqualTo(testMap.MapId));
        });

        await pair.RunTicksSync(300);

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(map), Is.False);
            Assert.That(children.Any(entities.EntityExists), Is.False);
            Assert.That(entities.EntityExists(body), Is.False);

            var ghost = player.AttachedEntity;
            Assert.That(entities.HasComponent<GhostComponent>(ghost));
            var mapId = entities.GetComponent<TransformComponent>(ghost!.Value).MapID;
            Assert.That(mapId, Is.Not.EqualTo(MapId.Nullspace));
            Assert.That(mapId, Is.Not.EqualTo(testMap.MapId));

            server.CfgMan.SetCVar(PerformanceCVars.MapDeletionBudgetMs, PerformanceCVars.MapDeletionBudgetMs.DefaultValue);
        });

        await pair.CleanReturnAsync();
    }
}

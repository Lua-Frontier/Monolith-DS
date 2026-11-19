/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Content.Server._Mono.GridClaimer;
using Content.Server._NF.StationEvents.Components;
using Content.Server.GameTicking;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class BluespaceGridCleanupTests
{
    [TestPrototypes]
    private const string Rule = """
        - type: entity
          parent: BaseGameRule
          id: LuaMTestBluespaceCleanupRule
          components:
          - type: BluespaceErrorRule
            groups: {}
        """;

    [TestCase(false, false, TestName = "UnclaimedEventGridIsRemoved")]
    [TestCase(true, false, TestName = "GridClaimedBeforeEventEndsSurvives")]
    [TestCase(false, true, TestName = "GridClaimedDuringDeferredCleanupSurvives")]
    public async Task EventGridCleanupHonorsClaim(bool claimBeforeEnd, bool claimAfterEnd)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            var claimer = server.System<GridClaimerSystem>();
            var rule = ticker.AddGameRule("LuaMTestBluespaceCleanupRule");
#pragma warning disable RA0002
            entities.GetComponent<BluespaceErrorRuleComponent>(rule).GridsUid.Add(map.Grid);
#pragma warning restore RA0002
            entities.EnsureComponent<ClaimableGridComponent>(map.Grid);
            var device = entities.SpawnEntity(null, map.GridCoords);
            var claiming = entities.EnsureComponent<GridClaimerComponent>(device);

            if (claimBeforeEnd)
                claimer.ClaimGrid(map.Grid, (device, claiming));

            Assert.That(ticker.EndGameRule(rule), Is.True);
            if (claimAfterEnd)
            {
                Assert.That(entities.EntityExists(map.Grid), Is.True);
                Assert.That(claimer.IsClaimable(map.Grid), Is.True);
                claimer.ClaimGrid(map.Grid, (device, claiming));
                Assert.That(entities.GetComponent<ClaimableGridComponent>(map.Grid).Claimed, Is.True);
            }
        });

        await server.WaitRunTicks(8);
        await server.WaitAssertion(() =>
            Assert.That(entities.EntityExists(map.Grid), Is.EqualTo(claimBeforeEnd || claimAfterEnd)));
        await pair.CleanReturnAsync();
    }
}

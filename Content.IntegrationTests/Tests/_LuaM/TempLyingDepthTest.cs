using System.Numerics;
using Content.Shared.Standing;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.IntegrationTests.Tests._LuaM;

public sealed class TempLyingDepthTest
{
    [Test]
    public async Task LyingMobGoesUnderBedsheet()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var sEnt = server.ResolveDependency<IEntityManager>();
        var cEnt = client.ResolveDependency<IEntityManager>();
        var standing = server.System<StandingStateSystem>();

        var map = await pair.CreateTestMap();
        EntityUid mob = default;
        EntityUid sheet = default;
        EntityUid dsheet = default;

        await server.WaitPost(() =>
        {
            var pos = new MapCoordinates(Vector2.Zero, map.MapId);
            mob = sEnt.SpawnEntity("MobHuman", pos);
            sheet = sEnt.SpawnEntity("BedsheetBlue", pos);
            dsheet = sEnt.SpawnEntity("DoubleBedsheetBlue", pos);
        });
        await pair.RunTicksSync(10);

        int Depth(EntityUid serverUid) =>
            cEnt.GetComponent<SpriteComponent>(cEnt.GetEntity(sEnt.GetNetEntity(serverUid))).DrawDepth;

        await client.WaitAssertion(() =>
        {
            Assert.That(Depth(mob), Is.EqualTo((int) DrawDepth.Mobs));
            Assert.That(Depth(sheet), Is.EqualTo((int) DrawDepth.BelowMobs));
            Assert.That(Depth(dsheet), Is.EqualTo((int) DrawDepth.BelowMobs));
        });

        await server.WaitPost(() => standing.Down(mob, playSound: false, dropHeldItems: false));
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() => Assert.That(Depth(mob), Is.EqualTo((int) DrawDepth.Items)));

        await server.WaitPost(() => standing.Stand(mob, force: true));
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() => Assert.That(Depth(mob), Is.EqualTo((int) DrawDepth.Mobs)));

        await pair.CleanReturnAsync();
    }
}

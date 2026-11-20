using System.Numerics;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class ThrowSweepDiagonalTests
{
    // -- Диагональное окно занимает нижнюю правую половину тайла (0,0); бросок проходит параллельно скосу через пустую половину
    [Test]
    public async Task ThrowPastDiagonalWindowIsNotSwept()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var transform = entMan.System<SharedTransformSystem>();
        var mapSys = entMan.System<SharedMapSystem>();
        var throwing = entMan.System<ThrowingSystem>();

        var map = await pair.CreateTestMap();
        var direction = Vector2.Normalize(new Vector2(1f, 1f));
        var start = new Vector2(-2f, -1.3f);
        EntityUid item = default;

        await server.WaitPost(() =>
        {
            var plating = tileDefs["Plating"].TileId;
            for (var x = -4; x <= 4; x++)
            for (var y = -4; y <= 4; y++)
                mapSys.SetTile(map.Grid, new Vector2i(x, y), new Tile(plating));

            entMan.SpawnEntity("ReinforcedWindowDiagonal", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            item = entMan.SpawnEntity("Wrench", new EntityCoordinates(map.Grid, start));
            throwing.TryThrow(item, direction * 8f, 20f, recoil: false, animated: false, playSound: false, doSpin: false);
        });

        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var travelled = Vector2.Dot(transform.GetWorldPosition(item) - start, direction);
            Assert.That(travelled, Is.GreaterThan(4f));
        });

        await pair.CleanReturnAsync();
    }
}

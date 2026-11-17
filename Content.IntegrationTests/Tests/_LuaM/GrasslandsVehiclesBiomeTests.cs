using System.Collections.Generic;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GrasslandsVehiclesBiomeTests
{
    private const string OreSpawner = "MonoPlanetmapOreBase";
    private const int Size = 300;

    [Test]
    public async Task OreWallsOnlyFormClusters()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var biome = server.System<SharedBiomeSystem>();

        await server.WaitAssertion(() =>
        {
            var layers = protoMan.Index<BiomeTemplatePrototype>("LuaMVehicleGrass").Layers;
            var grass = new Tile(tileDefs["FloorPlanetGrass"].TileId);
            var ore = new HashSet<Vector2i>();

            for (var x = -Size / 2; x < Size / 2; x++)
            {
                for (var y = -Size / 2; y < Size / 2; y++)
                {
                    var pos = new Vector2i(x, y);
                    if (biome.TryGetEntity(pos, layers, grass, 0, (Entity<MapGridComponent>?) null, out var proto) && proto == OreSpawner)
                        ore.Add(pos);
                }
            }

            Assert.That(ore, Is.Not.Empty);

            var lonely = 0;
            foreach (var pos in ore)
            {
                var neighbours = 0;
                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        if ((dx != 0 || dy != 0) && ore.Contains(pos + new Vector2i(dx, dy)))
                            neighbours++;
                    }
                }

                if (neighbours == 0)
                    lonely++;
            }

            Assert.That(lonely, Is.Zero, $"{lonely} of {ore.Count} ore walls have no neighbouring walls");
        });

        await pair.CleanReturnAsync();
    }
}

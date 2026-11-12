using System.Collections.Generic;
using System.Linq;
using Content.Client._LuaM.Shipyard;
using Content.Client._NF.Shipyard.UI;
using Content.Shared._NF.Shipyard.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class ShipyardPreviewTests
{
    private const string PreviewMapName = "Shipyard preview";

    private static int PreviewMaps(IEntityManager entities)
    {
        var count = 0;
        var query = entities.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityName == PreviewMapName)
                count++;
        }

        return count;
    }

    [Test]
    public async Task EveryVesselBuildsOnAClientOnlyMap()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var entities = client.ResolveDependency<IEntityManager>();
        var vessels = client.ResolveDependency<IPrototypeManager>().EnumeratePrototypes<VesselPrototype>().ToList();
        var preview = entities.System<ShipyardPreviewRenderSystem>();
        var failed = new List<string>();

        foreach (var vessel in vessels)
        {
            await client.WaitPost(() =>
            {
                var built = preview.TryBuildGrid(vessel, out var map, out var mapId, out var bounds, out var info);
                if (!built || !mapId.IsClientSide || info is not { Tiles: > 0 } || bounds.Width <= 0f || bounds.Height <= 0f)
                    failed.Add(vessel.ID);
                else
                    TestContext.Out.WriteLine($"PREVIEW {vessel.ID} {info.Dimensions.X}x{info.Dimensions.Y} tiles={info.Tiles}");

                entities.DeleteEntity(map);
            });
        }

        await pair.RunTicksSync(3);
        await client.WaitAssertion(() =>
        {
            Assert.That(vessels, Is.Not.Empty);
            Assert.That(failed, Is.Empty, $"Shipyard previews failed to build: {string.Join(", ", failed)}");
            Assert.That(PreviewMaps(entities), Is.Zero);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClosingTheConsoleLeavesNoPreviewMap()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var entities = client.ResolveDependency<IEntityManager>();
        var vessel = client.ResolveDependency<IPrototypeManager>().EnumeratePrototypes<VesselPrototype>().First();
        ShipyardConsoleMenu menu = null;

        await client.WaitPost(() =>
        {
            menu = new ShipyardConsoleMenu();
            menu.PopulateProducts(new() { vessel.ID }, new(), false, true);
            menu.OpenCentered();
        });

        await pair.RunTicksSync(10);
        await client.WaitPost(() => menu!.Close());
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            Assert.That(menu!.PreviewView.Vessel?.ID, Is.EqualTo(vessel.ID));
            Assert.That(PreviewMaps(entities), Is.Zero);
        });

        await pair.CleanReturnAsync();
    }
}

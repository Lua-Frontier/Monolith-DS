using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._LuaM.Shipyard;
using Content.Client._NF.Shipyard.UI;
using Content.Shared._NF.Shipyard.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class ShipyardPreviewTests
{
    [Test]
    public void LargeMapsHaveBoundedReducedResolutionAndAPathApi()
    {
        var pick = typeof(ShipyardPreviewRenderSystem).GetMethod("PickPixelsPerTile", BindingFlags.NonPublic | BindingFlags.Static)!;
        var scale = (int)pick.Invoke(null, new object[] { new Vector2(1500, 1500) })!;
        Assert.That(scale, Is.GreaterThan(0), "Large plans should reduce resolution rather than fail at a four-pixel minimum.");
        Assert.That(1500L * 1500 * scale * scale * 4, Is.LessThanOrEqualTo(32L * 1024 * 1024));
        Assert.That(typeof(ShipyardPreviewRenderSystem).GetMethod("TryBuildMap"), Is.Not.Null,
            "Stations and drones must be loaded by map path, without fake Vessel prototypes.");
    }

    [TestCase(200, 200)]
    [TestCase(1000, 1000)]
    [TestCase(1500, 1500)]
    [TestCase(2000, 2)]
    public void TextureFallbackHonoursBothBudgets(float width, float height)
    {
        var pick = typeof(ShipyardPreviewRenderSystem).GetMethod("PickPixelsPerTile", BindingFlags.NonPublic | BindingFlags.Static)!;
        var scale = (int)pick.Invoke(null, new object[] { new Vector2(width, height) })!;
        if (width == 200 && height == 200) Assert.That(scale, Is.GreaterThan(0));
        if (scale == 0) return; // Explicitly unsupported, never allocate an oversized target.
        var pixelsX = (long)Math.Ceiling(width * scale);
        var pixelsY = (long)Math.Ceiling(height * scale);
        Assert.That(pixelsX, Is.LessThanOrEqualTo(4096));
        Assert.That(pixelsY, Is.LessThanOrEqualTo(4096));
        Assert.That(pixelsX * pixelsY * 4, Is.LessThanOrEqualTo(32L * 1024 * 1024));
    }

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

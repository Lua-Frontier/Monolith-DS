/* LuaCorp - AGPLv3. See AGPLv3.txt for details. */
using Content.Client._LuaM.Guidebook;
using Content.Client._LuaM.Shipyard;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuidebookMapPreviewTests
{
    [TestCase("/SharedMaps/_Mono/Shuttles/World/ramdronesmall.yml")]
    [TestCase("/Maps/_LuaM/POI/MedicalLuaM.yml")]
    public async Task StationsAndDronesLoadLocallyAndAreDeleted(string path)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var before = entities.EntityCount;
            var preview = entities.System<ShipyardPreviewRenderSystem>();
            Assert.That(preview.TryBuildMap(path, out var map, out var id, out var bounds, out var info), Is.True);
            Assert.That(id.IsClientSide, Is.True);
            Assert.That(info, Is.Not.Null);
            Assert.That(info!.Tiles, Is.GreaterThan(0));
            Assert.That(bounds.Width, Is.GreaterThan(0));
            entities.DeleteEntity(map);
            Assert.That(entities.EntityCount, Is.EqualTo(before), "Preview must not leave entities outside its map.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClosingWindowCancelsItsRequestWithoutCancellingAnotherOwner()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var render = entities.System<ShipyardPreviewRenderSystem>();
            var preview = entities.System<GuidebookMapPreviewSystem>();
            const string path = "/SharedMaps/_Mono/Shuttles/World/ramdronesmall.yml";
            var other = new object();
            render.Request(path, other);
            preview.Open(path, "Drone");
            Assert.That(preview.IsLoading, Is.True);
            preview.Close();
            Assert.That(preview.IsLoading, Is.False);
            Assert.That(render.IsLoading(path), Is.True);
            render.Release(other);
            Assert.That(render.IsLoading(path), Is.False);
        });
        await pair.CleanReturnAsync();
    }
}

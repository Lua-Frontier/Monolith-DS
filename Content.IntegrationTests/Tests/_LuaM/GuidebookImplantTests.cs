/* LuaCorp - AGPLv3. See AGPLv3.txt for details. */
using System.Linq;
using Content.Server._LuaM.Guidebook;
using Content.Shared.CCVar;
using Content.Shared._LuaM.Guidebook;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuidebookImplantTests
{
    [Test]
    public async Task ImplantsResolveThroughActualAutosurgeonSupply()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.ResolveDependency<IEntityManager>();
            var before = entities.EntityCount;
            // PoolManager selects Empty. Test the actual standard lobby metadata without loading its entities.
            var configuration = pair.Server.ResolveDependency<IConfigurationManager>();
            var previousMap = configuration.GetCVar(CCVars.GameMap);
            GuidebookCatalog catalog;
            try
            {
                configuration.SetCVar(CCVars.GameMap, "Frontier");
                catalog = entities.System<GuidebookCatalogSystem>().BuildSnapshot();
            }
            finally
            {
                configuration.SetCVar(CCVars.GameMap, previousMap);
            }
            var implants = catalog.Acquisitions.Where(e => e.Kind == "implant").ToList();
            Assert.That(implants.Any(e => e.PrototypeId == "OrganBerserkHeart"), Is.True);
            Assert.That(implants.Any(e => e.PrototypeId == "OrganCNSKerenzikov"), Is.True);
            Assert.That(implants.Any(e => e.PrototypeId == "BioSynthLeftHand"), Is.True,
                "Medical biofabricator limbs are an obtainable alternative to cybernetics.");
            Assert.That(implants.Where(e => e.PrototypeId.Contains("MaxTac")), Is.Empty);
            Assert.That(implants.SelectMany(e => e.Offers).Any(o => o.Method == "fabricator"), Is.True);
            Assert.That(implants.Single(e => e.PrototypeId == "MindShieldImplant").Offers
                .Any(o => o.SourceId == "role:DeliveryCourier"), Is.True, "A real civilian lobby role supplies this implant.");
            foreach (var entry in implants)
                TestContext.Out.WriteLine("IMPLANT " + entry.PrototypeId + " | " + string.Join("; ", entry.Offers.Select(o => o.SourceName).Distinct()));
            Assert.That(entities.EntityCount, Is.EqualTo(before));
        });
        await pair.CleanReturnAsync();
    }
}

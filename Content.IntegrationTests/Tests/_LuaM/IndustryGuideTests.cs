/* LuaCorp - Industry guide regression tests.
 * This file is licensed under AGPLv3. See AGPLv3.txt for details. */
using System.Collections.Generic;
using System.Linq;
using Content.Client._LuaM.Guidebook;
using Content.Client.Guidebook;
using Content.Shared.Guidebook;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class IndustryGuideTests
{
    [Test]
    public async Task IndustryPagesAndNativeResearchGuideParse()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var data = pair.Client.ResolveDependency<IEntityManager>().System<GuidebookCatalogClientSystem>();
        await pair.Client.WaitPost(data.Request);
        for (var i = 0; i < 100 && data.Catalog == null; i++) await pair.RunTicksSync(5);
        await pair.Client.WaitAssertion(() =>
        {
            var parser = pair.Client.ResolveDependency<DocumentParsingManager>();
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            foreach (var id in new[] { "IndustryMachineParts", "IndustryDrilling", "IndustryFabricators", "IndustryResearch", "IndustryAutomation", "MMC", "ChimeraPracticalGuide", "CorticalBorerPracticalGuide",
                "ColossusAtmos", "ColossusPower", "ColossusCommodities", "ColossusGasTritium", "ColossusGasFrezon", "ColossusGasBZ", "ColossusGasHealium", "ColossusGasNitrium", "ColossusGasPluoxium", "LuaMWorldObjects", "LuaMAsteroids", "References", "LuaMHelios", "ColossusOrientation", "SectorTopology", "ColossusHandheldWeapons" })
            {
                using var page = new BoxContainer();
                Assert.That(parser.TryAddMarkup(page, new ProtoId<GuideEntryPrototype>(id)), Is.True, id);
            }


        });
        await pair.CleanReturnAsync();
    }
}

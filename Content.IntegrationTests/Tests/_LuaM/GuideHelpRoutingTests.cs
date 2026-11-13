/* LuaCorp - AGPLv3. See AGPLv3.txt. */
using System.Collections.Generic;
using System.Linq;
using Content.Client.Guidebook.Components;
using Content.Shared.Guidebook;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuideHelpRoutingTests
{
    [Test]
    public async Task InheritedEquipmentHelpAndLobbyRoutesReachExistingRelevantPages()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var resources = pair.Client.ResolveDependency<IResourceManager>();
            var guides = prototypes.EnumeratePrototypes<GuideEntryPrototype>().ToDictionary(g => g.ID);
            HashSet<string> Descendants(string id)
            {
                var result = new HashSet<string> { id };
                foreach (var child in guides[id].Children)
                    result.UnionWith(Descendants(child));
                return result;
            }
            var roles = Descendants("Factions");
            var species = Descendants("Species");
            void Check(string id, string owner)
            {
                Assert.That(guides.ContainsKey(id), Is.True, $"{owner}: missing guide {id}");
                Assert.That(resources.ContentFileExists(guides[id].Text), Is.True, $"{owner}: missing file for {id}");
            }
            foreach (var entity in prototypes.EnumeratePrototypes<EntityPrototype>())
            {
                var help = entity.Components.TryGetValue("GuideHelp", out var entry) ? (GuideHelpComponent) entry.Component : null;
                if (help != null)
                    foreach (var guide in help.Guides) Check(guide, entity.ID);
                foreach (var (component, target) in new[]
                {
                    ("AutoSurgeon", "ColossusSurgery"), ("ShipyardConsole", "ColossusCatalogueShipyards"),
                    ("FireControlConsole", "WeaponsSystems"), ("GasCanister", "ColossusAtmosEquipment"),
                    ("Mech", "ColossusMechsHub"), ("GasDepositExtractor", "GasMining"),
                    ("PartExchanger", "IndustryMachineParts"), ("SalvageExpeditionConsole", "Expeditions"),
                })
                {
                    if (!entity.Components.ContainsKey(component)) continue;
                    Assert.That(help, Is.Not.Null, entity.ID);
                    Assert.That(help!.Guides.Select(g => g.ToString()), Does.Contain(target), entity.ID);
                }
            }
            foreach (var job in prototypes.EnumeratePrototypes<JobPrototype>())
            foreach (var guide in job.Guides ?? new())
            {
                Check(guide, job.ID);
                Assert.That(roles, Does.Contain(guide.ToString()), $"{job.ID}: lobby help belongs under Roles");
            }
            foreach (var race in prototypes.EnumeratePrototypes<SpeciesPrototype>().Where(s => s.RoundStart))
            {
                Check(race.ID, race.ID);
                Assert.That(species, Does.Contain(race.ID), "The lobby selects the species guide by its stable ID.");
            }
            foreach (var (entity, guide) in new[]
            {
                ("ADSAsakimIDCard", "ColossusPrecursors"), ("PlayerBorgRedacted", "ColossusPrecursors"),
                ("PlayerStationAiRedacted", "ColossusPrecursors"), ("BookTSFSOP", "ColossusTSFCode"),
                ("BookNordfallSOP", "ColossusNordfallCharter"), ("OreProcessorIndustrialFlatpack", "ColossusMining"),
                ("StationLaserDrill", "ColossusLaserDrill"), ("MachineShipDrill", "IndustryDrilling"),
                ("MachineAPE", "ColossusAnomalies"), ("VendingMachineMMCUplink", "ColossusCatalogueUplinks"),
            })
            {
                var help = (GuideHelpComponent) prototypes.Index<EntityPrototype>(entity).Components["GuideHelp"].Component;
                Assert.That(help.Guides.Select(g => g.ToString()), Does.Contain(guide), entity);
            }
        });
        await pair.CleanReturnAsync();
    }
}

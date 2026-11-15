/* LuaCorp - AGPLv3. See AGPLv3.txt. */
using System.Collections.Generic;
using System.Linq;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Components;
using Content.Client.Guidebook.Controls;
using Content.Client.Guidebook.Richtext;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared.Guidebook;
using Content.Shared.Interaction;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class FactionSopGuideTests
{
    [Test]
    public async Task RoleHelpStartingGearAndBookActivationReachTheFactionCodes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var resources = pair.Client.ResolveDependency<IResourceManager>();
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var parser = pair.Client.ResolveDependency<DocumentParsingManager>();
            var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
            var system = entities.System<GuidebookSystem>();
            foreach (var (guide, parent, book, jobs) in new[]
            {
                ("ColossusTSFCode", "ColossusTSFRoles", "BookTSFSOP",
                    new[] { "Sheriff", "Bailiff", "TsfMarsoc", "SeniorOfficer", "Brigmedic", "Deputy", "TsfEngineer", "TsfBorg" }),
                ("ColossusNordfallCharter", "ColossusNordfallRoles", "BookNordfallSOP",
                    new[] { "PirateCaptain", "PirateFirstMate", "PDVTarkhan", "PDVDenasvar", "PDVInfiltrator", "Pirate", "PdvBorg" }),
            })
            {
                Assert.That(prototypes.Index<GuideEntryPrototype>(parent).Children.Select(c => c.ToString()), Does.Contain(guide));
                foreach (var id in jobs)
                {
                    var job = prototypes.Index<JobPrototype>(id);
                    Assert.That(job.Guides!.Select(g => g.ToString()), Does.Contain(parent), id);
                    if (job.JobEntity != null) continue;
                    var gear = prototypes.Index(job.StartingGear!.Value);
                    Assert.That(gear.Storage.Values.SelectMany(v => v).Count(e => e.Id == book), Is.EqualTo(1), id);
                }
                var entry = prototypes.Index<GuideEntryPrototype>(guide);
                using var reader = resources.ContentFileReadText(entry.Text);
                var xml = reader.ReadToEnd();
                Assert.That(xml, Does.Contain("10-100").And.Contain("45 до 60 минут").And.Contain("LuaMRuleset"));
                using var document = new Document();
                Assert.That(parser.TryAddMarkup(document, xml), Is.True, guide);

                ProtoId<GuideEntryPrototype>? opened = null;
                void Capture(List<ProtoId<GuideEntryPrototype>> guides, List<ProtoId<GuideEntryPrototype>>? roots,
                    ProtoId<GuideEntryPrototype>? selected, bool children, ProtoId<GuideEntryPrototype>? start)
                    => opened = start;
                var uid = entities.SpawnEntity(book, MapCoordinates.Nullspace);
                system.OnGuidebookOpen += Capture;
                try
                {
                    Assert.That(entities.GetComponent<GuideHelpComponent>(uid).OpenOnActivation, Is.True);
                    var activate = new ActivateInWorldEvent(uid, uid, true);
                    entities.EventBus.RaiseLocalEvent(uid, activate);
                    Assert.That(activate.Handled, Is.True, book);
                    Assert.That(opened?.ToString(), Is.EqualTo(guide), book);
                }
                finally
                {
                    system.OnGuidebookOpen -= Capture;
                    ui.GetUIController<GuidebookUIController>().CloseGuidebook();
                    entities.DeleteEntity(uid);
                }
            }
        });
        await pair.CleanReturnAsync();
    }
}

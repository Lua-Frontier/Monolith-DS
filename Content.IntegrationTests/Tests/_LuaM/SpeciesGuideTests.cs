/* LuaCorp - AGPLv3. See AGPLv3.txt. */
using System.Collections.Generic;
using System.Linq;
using Content.Client._LuaM.Guidebook;
using Content.Client.Guidebook;
using Content.Shared.Guidebook;
using Content.Shared.Humanoid.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class SpeciesGuideTests
{
    private static IEnumerable<Control> Descendants(Control root)
    {
        yield return root;
        foreach (var child in root.Children)
        foreach (var descendant in Descendants(child))
            yield return descendant;
    }

    [Test]
    public async Task EverySelectableRaceHasAReadableArticleWithItsOwnStats()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var parser = pair.Client.ResolveDependency<DocumentParsingManager>();
            var races = prototypes.EnumeratePrototypes<SpeciesPrototype>().Where(s => s.RoundStart).Select(s => s.ID).ToArray();
            ProtoId<GuideEntryPrototype> index = "Species";
            Assert.That(prototypes.Index(index).Children.Where(c => c.Id != "Flight").Select(c => c.Id),
                Is.EquivalentTo(races), "Race pages must match character creation; Flight is the shared ability guide.");
            foreach (var race in races)
            {
                using var page = new BoxContainer();
                ProtoId<GuideEntryPrototype> article = race;
                Assert.That(parser.TryAddMarkup(page, article), Is.True, race);
                var stats = Descendants(page).OfType<GuideSpeciesStats>().Single();
                Assert.That(stats.Children.OfType<GuideCatalogHeader>().Count(), Is.EqualTo(1), race);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpeciesPageSelectsOneRaceWithoutSpawningBodies()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var before = entities.EntityCount;
            using var human = new GuideSpeciesStats();
            Assert.That(human.TryParseTag(new Dictionary<string, string> { ["Species"] = "Human" }, out _), Is.True);
            Assert.That(human.Children.OfType<GuideCatalogHeader>().Count(), Is.EqualTo(1), "A race article must not contain the entire catalog.");
            using var moth = new GuideSpeciesStats();
            Assert.That(moth.TryParseTag(new Dictionary<string, string> { ["Species"] = "Moth" }, out _), Is.True);
            Assert.That(moth.Children.OfType<GuideCatalogHeader>().Count(), Is.EqualTo(1));
            using var unknown = new GuideSpeciesStats();
            Assert.That(unknown.TryParseTag(new Dictionary<string, string> { ["Species"] = "NotASpecies" }, out _), Is.False);
            Assert.That(entities.EntityCount, Is.EqualTo(before), "Statistics must read prototypes without creating mobs or organs.");
        });
        await pair.CleanReturnAsync();
    }
}

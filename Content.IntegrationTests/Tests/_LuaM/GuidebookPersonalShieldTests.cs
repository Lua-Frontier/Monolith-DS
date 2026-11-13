/* LuaCorp - AGPLv3. See AGPLv3.txt for details. */
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._LuaM.Guidebook;
using Content.Server._LuaM.Guidebook;
using Content.Shared._LuaM.Guidebook;
using Content.Shared._Mono.PersonalShield;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuidebookPersonalShieldTests
{
    [Test]
    public async Task AvailableArmorCarriesItsOwnShieldCharacteristics()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.ResolveDependency<IEntityManager>();
            var before = entities.EntityCount;
            var catalog = entities.System<GuidebookCatalogSystem>().BuildSnapshot();
            var shield = catalog.Acquisitions.FirstOrDefault(e => e.Kind == "armor" && e.PrototypeId == "ClothingOuterHardsuitUIEnforcerMKII");
            Assert.That(shield, Is.Not.Null, "The shield suit must come from real supply.");
            var stats = shield!.ShieldStats;
            Assert.That(stats.Single(s => s.Label == "Максимальный запас поля").Value, Is.EqualTo("600 ед."));
            Assert.That(stats.Single(s => s.Label == "Восстановление поднятого поля").Value, Is.EqualTo("6 ед./с"));
            Assert.That(stats.Single(s => s.Label == "Развёртывание").Value, Is.EqualTo("1 с"));
            Assert.That(stats.Single(s => s.Label == "Задержка после распада").Value, Is.EqualTo("30 с"));
            Assert.That(stats.Single(s => s.Label == "Самозаряд батареи").Value, Is.EqualTo("0,1 Дж/с"));
            Assert.That(stats.Single(s => s.Label == "Питание при непрерывной работе").Value, Does.Contain("150 с"));
            Assert.That(stats.Single(s => s.Label == "Бронеплиты").Value, Does.Contain("запрещена"));
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            Assert.That(catalog.Acquisitions.Where(e => e.Kind == "armor"
                && !prototypes.Index<EntityPrototype>(e.PrototypeId).Components.ContainsKey("PersonalShield"))
                .All(e => e.ShieldStats.Count == 0), Is.True, "Ordinary armor must not gain a fictitious shield.");
            var specialized = catalog.Acquisitions.First(e => e.Kind == "armor"
                && prototypes.Index<EntityPrototype>(e.PrototypeId).TryGetComponent<PersonalShieldComponent>(out var field)
                && field.Shield.BlockDamageModifier.Coefficients.Values.Any(v => v != 1));
            Assert.That(specialized.ShieldStats.Where(s => s.Label.StartsWith("Расход заряда:"))
                .Any(s => s.Value != "×1"), Is.True, "Damage costs are shield-specific, not suit armor percentages.");
            Assert.That(entities.EntityCount, Is.EqualTo(before));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShieldBlockArrivesAtClientAndFitsNarrowPagesWithoutEntities()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var entities = pair.Client.ResolveDependency<IEntityManager>();
        var system = entities.System<GuidebookCatalogClientSystem>();
        await pair.Client.WaitPost(system.Request);
        for (var i = 0; i < 100 && system.Catalog == null; i++) await pair.RunTicksSync(5);
        await pair.Client.WaitAssertion(() =>
        {
            Assert.That(system.Catalog, Is.Not.Null);
            var before = entities.EntityCount;
            using var block = new GuidePersonalShieldStats(system.Catalog!.Acquisitions
                .Single(e => e.PrototypeId == "ClothingOuterHardsuitUIEnforcerMKII").ShieldStats);
            var text = string.Join("\n", Labels(block).Select(l => l.GetMessage()));
            Assert.That(text, Does.Contain("Встроенный персональный щит"));
            Assert.That(text, Does.Contain("600 ед."));
            Assert.That(text, Does.Contain("0,1 Дж/с"));
            foreach (var width in new[] { 540, 380 })
            {
                block.Measure(new Vector2(width, 10000));
                block.Arrange(new UIBox2(0, 0, width, block.DesiredSize.Y));
                Assert.That(block.DesiredSize.X, Is.LessThanOrEqualTo(width));
            }
            Assert.That(entities.EntityCount, Is.EqualTo(before));
        });
        await pair.CleanReturnAsync();
    }

    private static IEnumerable<RichTextLabel> Labels(Control control)
    {
        if (control is RichTextLabel label) yield return label;
        foreach (var child in control.Children)
        foreach (var descendant in Labels(child)) yield return descendant;
    }
}

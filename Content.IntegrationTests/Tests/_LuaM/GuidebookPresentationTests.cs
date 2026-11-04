/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Content.Client._LuaM.Guidebook;
using Content.Shared._LuaM.Guidebook;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuidebookPresentationTests
{
    [Test]
    public void ServerSummaryDoesNotExposeBatteryImplementationDetails()
    {
        var item = new GuidebookEquipmentEntry { Kind = "server", Stats = new()
        {
            new() { Label = "Вычислительная мощность", Value = "42 ед." },
            new() { Label = "Лимит консолей", Value = "не ограничен сервером" },
            new() { Label = "Начальный заряд", Value = "0 Дж" },
            new() { Label = "Автозарядка", Value = "нет" },
            new() { Label = "КПД зарядки батареи", Value = "0.95" },
        } };
        var summary = GuidebookCatalogPresentation.Summary(item);
        Assert.That(summary.Select(s => s.Label), Is.EquivalentTo(new[] { "Мощность СУО", "Консоли" }));
        Assert.That(GuidebookCatalogPresentation.DamageValue("65 → 71.5 после множителя орудия"), Is.EqualTo("71.5"));
    }

    [Test]
    public async Task SelectedShipyardToolbarFitsNarrowGuide()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var system = pair.Client.ResolveDependency<IEntityManager>().System<GuidebookCatalogClientSystem>();
            var property = typeof(GuidebookCatalogClientSystem).GetProperty(nameof(GuidebookCatalogClientSystem.Catalog))!;
            var previous = system.Catalog;
            var data = new GuidebookCatalog();
            data.Vessels.Add(new GuidebookVesselEntry { Id = "test", Name = "Корабль" });
            data.Shipyards.Add(new GuidebookShipyardEntry
            {
                Id = "yard", PrototypeId = "ComputerShipyard", Name = "Верфь", Vessels = new() { "test" },
            });
            property.SetValue(system, data);
            try
            {
                using var catalog = new GuideCatalog();
                typeof(GuideCatalog).GetField("_yard", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(catalog, "yard");
                catalog.TryParseTag(new Dictionary<string, string>(), out _);
                catalog.Measure(new Vector2(380, 4000));
                catalog.Arrange(new UIBox2(0, 0, 380, catalog.DesiredSize.Y));
                Assert.That(catalog.DesiredSize.X, Is.LessThanOrEqualTo(380));
                foreach (var toolbar in catalog.Children.OfType<BoxContainer>().Where(c => c.Orientation == BoxContainer.LayoutOrientation.Horizontal))
                foreach (var child in toolbar.Children.Where(c => c.Visible))
                    Assert.That(child.Position.X + child.Size.X, Is.LessThanOrEqualTo(toolbar.Size.X + 0.1f),
                        "Back, page and refresh controls must remain readable on narrow guide pages.");
            }
            finally { property.SetValue(system, previous); }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReactorCalloutsUseMarkupInsteadOfLiteralBrackets()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            using var example = new GuideReactorUI();
            using var window = new Content.Client.UserInterface.Controls.FancyWindow();
            window.ContentsContainer.AddChild(new BoxContainer());
            var method = typeof(GuideReactorUI).GetMethod("AddInstructions", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var panel = (BoxContainer) method.Invoke(example, new object[] { window, "[bold]Важное[/bold]" })!;
            Assert.That(panel.Children.OfType<RichTextLabel>().Single().GetMessage(), Does.StartWith("[bold]"),
                "Literal markup is escaped by the string overload; this callout must contain a real bold tag.");
        });
        await pair.CleanReturnAsync();
    }
}

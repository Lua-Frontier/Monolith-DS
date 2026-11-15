/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Content.Client._LuaM.Guidebook;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Controls;
using Content.Client.Guidebook.Richtext;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Guidebook;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class ArmorGuideTests
{
    private static readonly Regex EntitiesRegex = new("Entities=\"([^\"]+)\"");

    [Test]
    public async Task ArmorDocumentsParseAndEveryComparedItemExists()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var resources = pair.Client.ResolveDependency<IResourceManager>();
            var parser = pair.Client.ResolveDependency<DocumentParsingManager>();
            foreach (var id in new[] { "ArmorExplanation", "ColossusArmorSuits", "ColossusArmorPlates", "ColossusPersonalAmmo", "ColossusCatalogueAmmo" })
            {
                var guide = prototypes.Index<GuideEntryPrototype>(id);
                using var reader = resources.ContentFileReadText(guide.Text);
                var xml = reader.ReadToEnd();
                foreach (Match match in EntitiesRegex.Matches(xml))
                foreach (var item in match.Groups[1].Value.Split(','))
                    Assert.That(prototypes.HasIndex<EntityPrototype>(item), Is.True, $"{id}: {item}");
                using var document = new Document();
                Assert.That(parser.TryAddMarkup(document, xml), Is.True, id);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AmmoComparisonShowsThermalStructuralHitscanAndPelletDataAndReleasesSprites()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var heat = prototypes.Index(new ProtoId<DamageTypePrototype>("Heat")).LocalizedName;
            var structural = prototypes.Index(new ProtoId<DamageTypePrototype>("Structural")).LocalizedName;
            var before = entities.EntityCount;
            using (var stats = new ArmorStats())
            {
                Assert.That(stats.TryParseTag(new Dictionary<string, string>
                {
                    ["Kind"] = "ammo",
                    ["Entities"] = "Cartridge635x40mmCaselessPlasma,Cartridge68x52mmCaselessPlasma,Cartridge145x114mm,ShellShotgun12_gaugeBuckshot",
                }, out _), Is.True);
                var table = (BoxContainer) stats.GetChild(0);
                var embeds = table.Children.SelectMany(row => row.Children).OfType<GuideEntityEmbed>().ToArray();
                Assert.That(embeds, Has.Length.EqualTo(4));
                foreach (var embed in embeds)
                {
                    var prototype = prototypes.Index(embed.EntityPrototypeId!.Value);
                    Assert.That(embed.Children.OfType<RichTextLabel>().Single().GetMessage(), Is.EqualTo(prototype.Name));
                }
                var text = string.Join("\n", Labels(stats).Select(label => label.GetFormattedMessage()?.ToString()));
                Assert.That(text, Does.Contain(heat + ": 12"));
                Assert.That(text, Does.Contain(heat + ": 15"));
                Assert.That(text, Does.Contain(structural + ": 35"));
                Assert.That(text, Does.Contain("-60%"));
                Assert.That(text, Does.Contain("-10%"));
                Assert.That(text, Does.Not.Contain("Нет обычного прямого урона"), "14.5mm must resolve HitscanBasicDamage.");
                CheckWidths(stats);
            }
            Assert.That(entities.EntityCount, Is.EqualTo(before), "Disposing comparisons releases their preview entities.");
            using var collapsed = new ArmorStats();
            collapsed.TryParseTag(new Dictionary<string, string>
            {
                ["Kind"] = "ammo", ["Title"] = "6.35x40mm", ["Entities"] = "Cartridge635x40mmCaselessPlasma",
            }, out _);
            Assert.That(entities.EntityCount, Is.EqualTo(before), "A closed caliber creates no sprite entities.");
            foreach (var (kind, ids) in new[]
            {
                ("suits", "ClothingOuterHardsuitM82c,ClothingOuterHardsuitUsspL10A,ClothingOuterHardsuitSyndieElite"),
                ("plates", "ArmorPlatePierce,ArmorPlateHeat,ArmorPlateSpeed"),
            })
            {
                using var comparison = new ArmorStats();
                comparison.TryParseTag(new Dictionary<string, string> { ["Kind"] = kind, ["Entities"] = ids }, out _);
                CheckWidths(comparison);
            }
        });
        await pair.CleanReturnAsync();
    }

    private static void CheckWidths(ArmorStats stats)
    {
        foreach (var width in new[] { 540, 380 })
        {
            stats.Measure(new Vector2(width, 10000));
            stats.Arrange(new UIBox2(0, 0, width, stats.DesiredSize.Y));
            Assert.That(stats.DesiredSize.X, Is.LessThanOrEqualTo(width));
            var table = stats.GetChild(0);
            foreach (var row in table.Children)
            foreach (var cell in row.Children)
                Assert.That(cell.Position.X + cell.Size.X, Is.LessThanOrEqualTo(width), "Comparison cells must remain inside the narrow guide page.");
        }
    }

    private static IEnumerable<RichTextLabel> Labels(Control control)
    {
        if (control is RichTextLabel label)
            yield return label;
        foreach (var child in control.Children)
        foreach (var descendant in Labels(child))
            yield return descendant;
    }
}

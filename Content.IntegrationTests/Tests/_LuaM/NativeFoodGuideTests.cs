/* LuaCorp - AGPLv3. See AGPLv3.txt. */
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Guidebook.Controls;
using Content.Shared.Kitchen;
using Content.Shared.Chemistry.Reaction;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class NativeFoodGuideTests
{
    private static IEnumerable<Control> Descendants(Control root)
    {
        yield return root;
        foreach (var child in root.Children)
        foreach (var nested in Descendants(child))
            yield return nested;
    }

    [Test]
    public async Task IngredientsStayCompactAndCookingInfoFollowsTheList()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var results = new[] { "FoodBakedCannoli", "FoodPieAmanita", "FoodBakedPancakeBb" };
            foreach (var result in results)
            {
                var recipe = prototypes.EnumeratePrototypes<FoodRecipePrototype>().First(r => r.Result.Id == result);
                foreach (var width in new[] { 360, 480 })
                {
                    using var card = new GuideMicrowaveEmbed(recipe);
                    card.Measure(new Vector2(width, float.PositiveInfinity));
                    card.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(width, card.DesiredSize.Y)));
                    var list = Descendants(card).Single(c => c.Name == "IngredientsList");
                    foreach (var row in list.Children)
                    {
                        Assert.That(row.Size.Y, Is.LessThanOrEqualTo(48), result + ": ingredient rows must not reserve gallery-sized image space.");
                        var cells = row.Children.ToArray();
                        for (var i = 1; i < cells.Length; i++)
                            Assert.That(cells[i].Position.X, Is.GreaterThanOrEqualTo(cells[i - 1].Position.X + cells[i - 1].Size.X), result + ": ingredient cells overlap.");
                    }
                    static float Top(Control child, Control ancestor)
                    {
                        var y = child.Position.Y;
                        for (var p = child.Parent; p != null && p != ancestor; p = p.Parent) y += p.Position.Y;
                        return y;
                    }
                    var cookTime = Descendants(card).Single(c => c.Name == "CookTimeLabel");
                    Assert.That(Top(cookTime, card), Is.GreaterThanOrEqualTo(Top(list, card) + list.Size.Y), result + ": cooking info must be below all ingredients.");
                }
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NativeRecipeCardsFitANarrowGuidePage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            void CheckBounds(Control card, string context)
            {
                card.Measure(new Vector2(360, float.PositiveInfinity));
                Assert.That(card.DesiredSize.X, Is.LessThanOrEqualTo(360), context);
                card.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(360, card.DesiredSize.Y)));
                foreach (var text in Descendants(card).OfType<RichTextLabel>())
                {
                    var offset = text.Position;
                    for (var parent = text.Parent; parent != null && parent != card; parent = parent.Parent)
                        offset += parent.Position;
                    Assert.That(offset.X + text.Size.X, Is.LessThanOrEqualTo(card.Size.X + 1), context + ": " + text.Name);
                }
            }
            foreach (var recipe in new[] { "RecipeTonkatsuNoodles", "RecipeMochiChocolate", "RecipeDonutJellyCaramel" })
            {
                using var card = new GuideMicrowaveEmbed(recipe);
                CheckBounds(card, recipe);
            }
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            ProtoId<ReactionPrototype> pinkDrink = "PinkDrink";
            using var reaction = new GuideReagentReaction(prototypes.Index(pinkDrink), prototypes,
                pair.Client.ResolveDependency<IEntitySystemManager>());
            CheckBounds(reaction, "PinkDrink reaction");
            using var drink = new GuideReagentEmbed("PinkDrink");
            CheckBounds(drink, "PinkDrink card");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RecipesUseTheExistingFoodGroups()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            foreach (var (id, group) in new[] { ("RecipeFoodRiceDough", "Ingredients"), ("RecipeRiceNoodleSheet", "Ingredients"),
                         ("RecipeCottonSalad", "Moth"), ("RecipeBananaBread", "Cake"), ("RecipeFoodBakedWholeChicken", "Savory") })
            {
                ProtoId<FoodRecipePrototype> recipe = id;
                Assert.That(prototypes.Index(recipe).Group, Is.EqualTo(group), id);
            }
        });
        await pair.CleanReturnAsync();
    }
}

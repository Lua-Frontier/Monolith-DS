#nullable enable
using Content.Shared.Humanoid;

namespace Content.IntegrationTests.Tests._LuaM;

/// <summary>
/// A profile loaded from a file used to keep any height and width, past the limits of the editor sliders.
/// </summary>
[TestFixture]
public sealed class CharacterSizeLimitTest
{
    [Test]
    public async Task SizeIsClampedOnValidation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var appearance = HumanoidCharacterAppearance.DefaultWithSpecies("Human");

            var huge = HumanoidCharacterAppearance.EnsureValid(appearance.WithHeight(5f).WithWidth(0.1f), "Human", Sex.Male);
            Assert.That(huge.Height, Is.EqualTo(HumanoidCharacterAppearance.MaxSize));
            Assert.That(huge.Width, Is.EqualTo(HumanoidCharacterAppearance.MinSize));

            var broken = HumanoidCharacterAppearance.EnsureValid(appearance.WithHeight(float.NaN).WithWidth(float.PositiveInfinity), "Human", Sex.Male);
            Assert.That(broken.Height, Is.EqualTo(1f));
            Assert.That(broken.Width, Is.EqualTo(1f));

            var normal = HumanoidCharacterAppearance.EnsureValid(appearance.WithHeight(1.1f).WithWidth(0.9f), "Human", Sex.Male);
            Assert.That(normal.Height, Is.EqualTo(1.1f));
            Assert.That(normal.Width, Is.EqualTo(0.9f));
        });

        await pair.CleanReturnAsync();
    }
}

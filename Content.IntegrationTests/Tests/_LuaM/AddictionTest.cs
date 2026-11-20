using System.Linq;
using Content.Server._LuaM.Body;
using Content.Server.Body.Systems;
using Content.Shared._LuaM.Traits;
using Content.Shared.Chemistry.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._LuaM;

/// <summary>
/// A body picks up several addictions in a round, on top of the dependency from the trait.
/// </summary>
[TestFixture]
public sealed class AddictionTest
{
    [Test]
    public async Task SeveralAddictionsStack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();

        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            human = entities.SpawnEntity("MobHuman", map.GridCoords);

            // the trait, with a chemical none of the addictions use
            entities.AddComponent(human, new ChemicalDependencyComponent { Reagent = "Bicaridine" });
            Assert.That(entities.GetComponent<ChemicalDependencyComponent>(human).Dependencies, Has.Count.EqualTo(1));

            // an omnizine overdose
            var blood = new Solution();
            blood.AddReagent("Omnizine", 70);
            Assert.That(entities.System<BloodstreamSystem>().TryAddToChemicals(human, blood));
            Metabolize(entities, human, "Omnizine", 1);

            // a pack of cigarettes and a bottle
            Metabolize(entities, human, "Nicotine", 35);
            Metabolize(entities, human, "Ethanol", 80);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var addictions = entities.GetComponent<ChemicalDependencyComponent>(human).Dependencies
                .Where(d => d.Addiction != null)
                .Select(d => d.Addiction!.Value.Id)
                .ToList();

            Assert.That(addictions, Is.EquivalentTo(new[] { "Omnizine", "Nicotine", "Alcohol" }));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The whole way: nicotine in the blood gets metabolized by the body itself, after an omnizine addiction.
    /// </summary>
    [Test]
    public async Task NicotineInBloodAfterOmnizine()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        var bloodstream = entities.System<BloodstreamSystem>();

        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            human = entities.SpawnEntity("MobHuman", map.GridCoords);
            entities.AddComponent(human, new ChemicalDependencyComponent { Reagent = "Bicaridine" });

            var omnizine = new Solution();
            omnizine.AddReagent("Omnizine", 70);
            Assert.That(bloodstream.TryAddToChemicals(human, omnizine));
        });

        await server.WaitRunTicks(30 * 5);

        await server.WaitAssertion(() =>
        {
            var nicotine = new Solution();
            nicotine.AddReagent("Nicotine", 35);
            Assert.That(bloodstream.TryAddToChemicals(human, nicotine));
        });

        // 35 units at half a unit a second
        await server.WaitRunTicks(30 * 90);

        await server.WaitAssertion(() =>
        {
            var addictions = entities.GetComponent<ChemicalDependencyComponent>(human).Dependencies
                .Where(d => d.Addiction != null)
                .Select(d => d.Addiction!.Value.Id)
                .ToList();

            Assert.That(addictions, Is.EquivalentTo(new[] { "Omnizine", "Nicotine" }));
        });

        await pair.CleanReturnAsync();
    }

    private static void Metabolize(IEntityManager entities, EntityUid body, string reagent, int amount)
    {
        var ev = new ReagentMetabolizedEvent(reagent, amount);
        entities.EventBus.RaiseLocalEvent(body, ref ev);
    }
}

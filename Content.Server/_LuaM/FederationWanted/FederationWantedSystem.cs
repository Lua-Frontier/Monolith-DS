using Content.Server.Radio.EntitySystems;
using Content.Shared._LuaM.FederationWanted;
using Content.Shared.Contraband;
using Content.Shared.Store;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.FederationWanted;

/// <summary>
/// The TSF side of the Corporate Agent trait: announces wanted people and lists them on the console.
/// </summary>
public sealed partial class FederationWantedSystem : EntitySystem
{
    private static readonly ProtoId<CurrencyPrototype> RewardCurrency = "FederationMilitaryCredit";

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FederationWantedComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FederationWantedConsoleComponent, BoundUIOpenedEvent>(OnConsoleOpened);
    }

    private void OnMapInit(Entity<FederationWantedComponent> ent, ref MapInitEvent args)
    {
        var state = GetState();
        var reward = GetReward(ent);
        var announced = false;

        var query = EntityQueryEnumerator<FederationWantedConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            _ui.SetUiState(uid, FederationWantedConsoleUiKey.Key, state);

            // every console hears the same channel, one report is enough
            if (announced)
                continue;

            announced = true;
            _proto.TryIndex(console.Language, out var language);
            _radio.SendRadioMessage(uid,
                Loc.GetString(console.Message, ("target", Name(ent)), ("reward", reward)),
                console.RadioChannel,
                uid,
                language: language);
        }
    }

    private void OnConsoleOpened(Entity<FederationWantedConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        _ui.SetUiState(ent.Owner, FederationWantedConsoleUiKey.Key, GetState());
    }

    private FederationWantedConsoleState GetState()
    {
        var entries = new List<FederationWantedEntry>();
        var query = EntityQueryEnumerator<FederationWantedComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            entries.Add(new FederationWantedEntry(Name(uid), GetReward(uid)));
        }

        return new FederationWantedConsoleState(entries);
    }

    private int GetReward(EntityUid uid)
    {
        return TryComp<ContrabandComponent>(uid, out var contraband)
            ? contraband.TurnInValues.GetValueOrDefault(RewardCurrency)
            : 0;
    }
}

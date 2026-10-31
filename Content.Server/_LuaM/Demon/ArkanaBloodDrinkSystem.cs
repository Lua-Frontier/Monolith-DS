using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Popups;
using Content.Shared._LuaM.Demon;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._LuaM.Demon;

public sealed partial class ArkanaBloodDrinkSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private StomachSystem _stomach = default!;

    private static readonly ProtoId<SpeciesPrototype> ArkanaSpecies = "Demon";
    private static readonly ProtoId<ReagentPrototype> AriralBlood = "AriralBlood";
    private static readonly ProtoId<DamageTypePrototype> Piercing = "Piercing";
    private static readonly TimeSpan DrinkDelay = TimeSpan.FromSeconds(3);
    private static readonly FixedPoint2 DrainAmount = FixedPoint2.New(25);
    private const float BiteDamage = 0.7f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodstreamComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<HumanoidAppearanceComponent, ArkanaBloodDrinkDoAfterEvent>(OnDoAfter);
    }

    private void OnGetVerbs(Entity<BloodstreamComponent> target, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.User == args.Target)
            return;

        if (!CanDrink(args.User, target))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("arkana-verb-drink-blood"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/drink.svg.192dpi.png")),
            Act = () => StartDrink(user, target),
            Priority = -1,
        });
    }

    private bool CanDrink(EntityUid user, EntityUid target)
    {
        if (!TryComp<HumanoidAppearanceComponent>(user, out var userAppearance) || userAppearance.Species != ArkanaSpecies)
            return false;

        return TryComp<HumanoidAppearanceComponent>(target, out var targetAppearance)
            && targetAppearance.Species != ArkanaSpecies;
    }

    private void StartDrink(EntityUid user, EntityUid target)
    {
        var args = new DoAfterArgs(EntityManager, user, DrinkDelay, new ArkanaBloodDrinkDoAfterEvent(), user, target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.25f,
            DistanceThreshold = 1.25f,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return;

        _damageable.TryChangeDamage(target, new DamageSpecifier(_proto.Index(Piercing), BiteDamage), interruptsDoAfters: false, origin: user);
        _popup.PopupEntity(Loc.GetString("arkana-drink-start", ("user", user), ("target", target)), user, PopupType.MediumCaution);
    }

    private void OnDoAfter(Entity<HumanoidAppearanceComponent> user, ref ArkanaBloodDrinkDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        if (!CanDrink(user, target) || !TryComp<BloodstreamComponent>(target, out var bloodstream))
            return;

        if (bloodstream.BloodReagent == AriralBlood)
        {
            _popup.PopupEntity(Loc.GetString("arkana-drink-fail-ariral"), user, user);
            return;
        }

        if (!_solutions.ResolveSolution(target, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var blood))
            return;

        var take = FixedPoint2.Min(DrainAmount, blood.Volume);
        if (take <= FixedPoint2.Zero)
            return;

        var protein = take / 2;
        var food = new Solution();
        food.AddReagent("Protein", protein);
        food.AddReagent("Saline", take - protein);

        if (!_body.TryGetBodyOrganEntityComps<StomachComponent>(user.Owner, out var stomachs))
            return;

        foreach (var stomach in stomachs)
        {
            if (!_stomach.CanTransferSolution(stomach.Owner, food, stomach.Comp1))
                continue;

            _solutions.SplitSolution(bloodstream.BloodSolution.Value, take);
            _stomach.TryTransferSolution(stomach.Owner, food, stomach.Comp1);
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("arkana-drink-success"), user, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("arkana-drink-fail-full"), user, user);
    }
}

using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Server.Cargo.Systems;
using Content.Server.Nutrition.EntitySystems;
using Content.Server.Stack;
using Content.Shared._LuaM.Stacks;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Nutrition;
using Content.Shared.Random;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server._LuaM.Stacks;

/// <summary>
/// Stacks of things used one at a time, produce and glass shards: identical units merge into one entity,
/// and a unit split off a produce stack keeps the seed and contents.
/// </summary>
/// <remarks>
/// Everything that eats, slices, extracts, injects, welds or crafts with these expects a single item. So a hand only
/// ever holds one unit (the rest goes back where it was taken from), and a stack that has something done to it on
/// the floor or in a bag first drops the rest into a separate pile.
/// </remarks>
public sealed class UnitStackSystem : EntitySystem
{
    [Dependency] private BotanySystem _botany = default!;
    [Dependency] private PricingSystem _pricing = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StackMagnetSystem _magnet = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    /// <summary>
    /// How long a unit that something is being done to stays out of the magnet, so a slicing or injecting
    /// do-after isn't cut short by the unit merging back into the pile.
    /// </summary>
    private static readonly TimeSpan WorkedOnTime = TimeSpan.FromSeconds(15);

    private bool _pricingUnit;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UnitStackComponent, StackMergeAttemptEvent>(OnMergeAttempt);
        SubscribeLocalEvent<UnitStackComponent, StackSplitEvent>(OnSplit);
        SubscribeLocalEvent<UnitStackComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<UnitStackComponent, EntGotRemovedFromContainerMessage>(OnRemovedFromContainer);
        SubscribeLocalEvent<UnitStackComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<UnitStackComponent, InteractUsingEvent>(OnInteractUsing,
            before: new[] { typeof(SliceableFoodSystem), typeof(LogSystem) });
        SubscribeLocalEvent<UnitStackComponent, ConsumeDoAfterEvent>(OnConsume, before: new[] { typeof(FoodSystem) });
        SubscribeLocalEvent<UnitStackComponent, PriceCalculationEvent>(OnPriceCalculation);
    }

    private void OnMergeAttempt(Entity<UnitStackComponent> ent, ref StackMergeAttemptEvent args)
    {
        if (!IsSameUnit(args.Donor, ent))
            args.Cancelled = true;
    }

    private void OnSplit(Entity<UnitStackComponent> ent, ref StackSplitEvent args)
    {
        _botany.CopyProduce(ent, args.NewId);
    }

    /// <summary>
    /// Remembers the floor spot right before a pickup. Only a field is written, so it is fine that this is also
    /// raised by mere "can it be picked up" checks: the spot is still where the stack lies.
    /// </summary>
    private void OnPickupAttempt(Entity<UnitStackComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        var xform = Transform(ent);
        if (xform.ParentUid != xform.GridUid && xform.ParentUid != xform.MapUid)
            return;

        ent.Comp.FloorOrigin = xform.Coordinates;
        ent.Comp.ContainerOrigin = null;
    }

    private void OnRemovedFromContainer(Entity<UnitStackComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        ent.Comp.ContainerOrigin = (args.Container.Owner, args.Container.ID);
        ent.Comp.FloorOrigin = null;
    }

    /// <summary>
    /// A hand keeps one unit, the rest goes back to the bag or the floor spot it was taken from.
    /// </summary>
    private void OnInsertedIntoContainer(Entity<UnitStackComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        var holder = args.Container.Owner;
        if (!HasComp<HandsComponent>(holder) || !_hands.IsHolding(holder, ent.Owner))
            return;

        var containerOrigin = ent.Comp.ContainerOrigin;
        var floorOrigin = ent.Comp.FloorOrigin;
        ent.Comp.ContainerOrigin = null;
        ent.Comp.FloorOrigin = null;

        if (!TryComp<StackComponent>(ent, out var stack) || stack.Count <= 1)
            return;

        if (containerOrigin is { } origin
            && !TerminatingOrDeleted(origin.Owner)
            && _container.TryGetContainer(origin.Owner, origin.Id, out var container))
        {
            SplitRestInto(ent, stack, container);
            return;
        }

        var coords = floorOrigin is { } floor && !TerminatingOrDeleted(floor.EntityId) && floor.IsValid(EntityManager)
            ? floor
            : _transform.GetMoverCoordinates(holder);
        _stack.Split(ent, stack.Count - 1, coords, stack);
    }

    /// <summary>
    /// Whatever is used on a stack (a knife, a syringe, an axe) gets a single unit; the rest becomes its own pile.
    /// Merging another stack of the same produce and scooping the stack into a bag still take the whole stack.
    /// </summary>
    private void OnInteractUsing(EntityUid uid, UnitStackComponent component, InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<StackComponent>(uid, out var stack) || stack.Count <= 1)
            return;

        if (TryComp<StackComponent>(args.Used, out var used) && used.StackTypeId == stack.StackTypeId
            || HasComp<StorageComponent>(args.Used))
            return;

        // A welder refines the whole stack, as far as its fuel goes.
        if (TryComp<ToolRefinableComponent>(uid, out var refinable))
        {
            string quality = refinable.QualityNeeded; // only read the component's field, it is restricted to its system
            if (_tool.HasQuality(args.Used, quality))
                return;
        }

        DetachRest(uid);
    }

    /// <summary>
    /// Eating from a pile on the floor (animals do that) eats one fruit, bite by bite.
    /// </summary>
    private void OnConsume(Entity<UnitStackComponent> ent, ref ConsumeDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        DetachRest(ent);
    }

    /// <summary>
    /// Leaves a single unit in <paramref name="uid"/> and moves the rest of the stack into a separate pile next to it,
    /// or back into the container it is in.
    /// </summary>
    public void DetachRest(EntityUid uid)
    {
        if (!HasComp<UnitStackComponent>(uid) || !TryComp<StackComponent>(uid, out var stack) || stack.Count <= 1)
            return;

        if (_container.TryGetContainingContainer((uid, null, null), out var container))
            SplitRestInto(uid, stack, container);
        else
            _stack.Split(uid, stack.Count - 1, Transform(uid).Coordinates, stack);

        _magnet.Block(uid, WorkedOnTime);
    }

    /// <summary>
    /// For crafting that takes the whole entity: returns one unit split off a stack of several,
    /// or the entity itself when it is a single item or not a unit stack.
    /// </summary>
    public EntityUid TakeOne(EntityUid uid, EntityCoordinates coords)
    {
        if (!HasComp<UnitStackComponent>(uid) || !TryComp<StackComponent>(uid, out var stack) || stack.Count <= 1)
            return uid;

        return _stack.Split(uid, 1, coords, stack) ?? uid;
    }

    /// <summary>
    /// The price of one unit times the stack size: the solution and static prices only count a single unit.
    /// </summary>
    private void OnPriceCalculation(Entity<UnitStackComponent> ent, ref PriceCalculationEvent args)
    {
        if (_pricingUnit || args.Handled || !TryComp<StackComponent>(ent, out var stack) || stack.Count <= 1)
            return;

        _pricingUnit = true;
        try
        {
            args.Price += _pricing.GetPrice(ent, includeContents: false) * (stack.Count - 1);
        }
        finally
        {
            _pricingUnit = false;
        }
    }

    private void SplitRestInto(EntityUid uid, StackComponent stack, BaseContainer container)
    {
        var rest = _stack.Split(uid, stack.Count - 1, _transform.GetMoverCoordinates(container.Owner), stack);
        if (rest == null)
            return;

        if (TryComp<StorageComponent>(container.Owner, out var storage) && storage.Container == container)
            _storage.Insert(container.Owner, rest.Value, out _, storageComp: storage, playSound: false);
        else
            _container.Insert(rest.Value, container);
    }

    /// <summary>
    /// Same name and, for produce, same genes and same contents, so one unit of the merged stack stands for any of them.
    /// </summary>
    private bool IsSameUnit(EntityUid a, EntityUid b)
    {
        if (Name(a) != Name(b))
            return false;

        var hasA = TryComp<ProduceComponent>(a, out var produceA);
        var hasB = TryComp<ProduceComponent>(b, out var produceB);
        if (hasA != hasB)
            return false;

        if (produceA == null || produceB == null)
            return true;

        _botany.TryGetSeed(produceA, out var seedA);
        _botany.TryGetSeed(produceB, out var seedB);
        return IsSameSeed(seedA, seedB)
            && IsSameSolution(a, produceA.SolutionName, b, produceB.SolutionName);
    }

    private static bool IsSameSeed(SeedData? a, SeedData? b)
    {
        if (ReferenceEquals(a, b))
            return true;

        if (a == null || b == null)
            return false;

        // SeedData only lets other systems read its fields, so copy them out before calling anything on them.
        string nameA = a.Name, nameB = b.Name;
        bool mysteriousA = a.Mysterious, mysteriousB = b.Mysterious;
        float potencyA = a.Potency, potencyB = b.Potency;
        Dictionary<string, SeedChemQuantity> chemicalsA = a.Chemicals, chemicalsB = b.Chemicals;
        List<RandomPlantMutation> mutationsA = a.Mutations, mutationsB = b.Mutations;

        if (nameA != nameB
            || mysteriousA != mysteriousB
            || potencyA != potencyB
            || chemicalsA.Count != chemicalsB.Count
            || mutationsA.Count != mutationsB.Count)
            return false;

        foreach (var (chem, quantity) in chemicalsA)
        {
            if (!chemicalsB.TryGetValue(chem, out var other)
                || quantity.Min != other.Min
                || quantity.Max != other.Max
                || quantity.PotencyDivisor != other.PotencyDivisor)
                return false;
        }

        for (var i = 0; i < mutationsA.Count; i++)
        {
            if (mutationsA[i].Name != mutationsB[i].Name)
                return false;
        }

        return true;
    }

    private bool IsSameSolution(EntityUid a, string nameA, EntityUid b, string nameB)
    {
        var hasA = _solution.TryGetSolution(a, nameA, out _, out var solutionA);
        var hasB = _solution.TryGetSolution(b, nameB, out _, out var solutionB);
        if (hasA != hasB)
            return false;

        if (solutionA == null || solutionB == null)
            return true;

        if (solutionA.Volume != solutionB.Volume || solutionA.Contents.Count != solutionB.Contents.Count)
            return false;

        foreach (var quantity in solutionA.Contents)
        {
            if (solutionB.GetReagentQuantity(quantity.Reagent) != quantity.Quantity)
                return false;
        }

        return true;
    }
}

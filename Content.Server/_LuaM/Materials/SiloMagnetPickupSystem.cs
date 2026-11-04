using Content.Shared._LuaM.Materials;
using Content.Shared.Examine;
using Content.Shared.Interaction.Components;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Materials;
using Content.Shared.Materials.OreSilo;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Whitelist;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Materials;

/// <summary>
/// Handles <see cref="SiloMagnetPickupComponent"/>: items on the floor around the machine go straight into its linked silo.
/// </summary>
public sealed partial class SiloMagnetPickupSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedMaterialStorageSystem _materialStorage = default!;
    [Dependency] private SharedOreSiloSystem _silo = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;

    private static readonly TimeSpan ScanDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan IdleScanDelay = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// The same cap as the other magnets have, so a big pile of ore doesn't take a whole tick.
    /// </summary>
    private const int MaxEntitiesPerScan = 15;

    private readonly HashSet<EntityUid> _nearby = new();

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<MaterialStorageComponent> _storageQuery;
    private EntityQuery<MaterialComponent> _materialQuery;
    private EntityQuery<PhysicalCompositionComponent> _compositionQuery;

    public override void Initialize()
    {
        base.Initialize();

        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _storageQuery = GetEntityQuery<MaterialStorageComponent>();
        _materialQuery = GetEntityQuery<MaterialComponent>();
        _compositionQuery = GetEntityQuery<PhysicalCompositionComponent>();

        SubscribeLocalEvent<SiloMagnetPickupComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SiloMagnetPickupComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<SiloMagnetPickupComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnMapInit(Entity<SiloMagnetPickupComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextScan = _timing.CurTime + _random.NextFloat() * ScanDelay;
    }

    private void OnToggled(Entity<SiloMagnetPickupComponent> ent, ref ItemToggledEvent args)
    {
        if (args.Activated)
            ent.Comp.NextScan = _timing.CurTime;
    }

    private void OnExamined(Entity<SiloMagnetPickupComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var key = !TryComp<OreSiloClientComponent>(ent, out var client) || client.Silo == null
            ? "silo-magnet-examine-not-linked"
            : TryGetSilo(ent, client, out _)
                ? "silo-magnet-examine-linked"
                : "silo-magnet-examine-unreachable";

        args.PushMarkup(Loc.GetString(key));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SiloMagnetPickupComponent, OreSiloClientComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var magnet, out var client, out var xform))
        {
            if (magnet.NextScan > now)
                continue;

            magnet.NextScan = now + IdleScanDelay;

            if (!_toggle.IsActivated(uid) || !_power.IsPowered(uid))
                continue;

            if (!TryGetSilo(uid, client, out var silo))
                continue;

            var position = _transform.GetWorldPosition(xform);
            var rangeSquared = magnet.Range * magnet.Range;

            // --  Approximate убирает точную проверку фикстур у всего подряд в радиусе, расстояние считаем сами и только для подошедших
            _nearby.Clear();
            _lookup.GetEntitiesInRange(uid, magnet.Range, _nearby, LookupFlags.Approximate | LookupFlags.Dynamic | LookupFlags.Sundries);

            var count = 0;
            foreach (var near in _nearby)
            {
                if (count >= MaxEntitiesPerScan)
                    break;

                if (near == xform.ParentUid || !_materialQuery.HasComp(near))
                    continue;

                // Only items lying around, not ones in flight or in someone's hands.
                if (!_physicsQuery.TryComp(near, out var physics) || physics.BodyStatus != BodyStatus.OnGround)
                    continue;

                if (_whitelist.IsWhitelistFail(magnet.Whitelist, near))
                    continue;

                if ((_transform.GetWorldPosition(near) - position).LengthSquared() > rangeSquared)
                    continue;

                if (TryInsertIntoSilo(silo, near))
                    count++;
            }

            if (count > 0)
                magnet.NextScan = now + ScanDelay;
        }
    }

    /// <summary>
    /// The silo the machine is linked to, if it is powered, on the same grid and in range.
    /// </summary>
    private bool TryGetSilo(EntityUid uid, OreSiloClientComponent client, out Entity<MaterialStorageComponent> silo)
    {
        silo = default;
        if (client.Silo is not { } siloUid
            || !_storageQuery.TryComp(siloUid, out var storage)
            || !_silo.CanTransmitMaterials(siloUid, uid))
            return false;

        silo = (siloUid, storage);
        return true;
    }

    /// <summary>
    /// Puts the materials of the item into the silo and deletes it. Unlike inserting by hand there are no popups and sounds,
    /// the magnet moves a lot of ore and the silo is usually far from it.
    /// </summary>
    private bool TryInsertIntoSilo(Entity<MaterialStorageComponent> silo, EntityUid item)
    {
        if (!_compositionQuery.TryComp(item, out var composition) || !HasComp<MaterialComponent>(item))
            return false;

        if (_whitelist.IsWhitelistFail(silo.Comp.Whitelist, item) || HasComp<UnremoveableComponent>(item))
            return false;

        var multiplier = TryComp<StackComponent>(item, out var stack) ? stack.Count : 1;
        var totalVolume = 0;
        foreach (var (material, volume) in composition.MaterialComposition)
        {
            if (!_materialStorage.CanChangeMaterialAmount(silo, material, volume * multiplier, silo.Comp))
                return false;

            totalVolume += volume * multiplier;
        }

        if (!_materialStorage.CanTakeVolume(silo, totalVolume, silo.Comp, localOnly: true))
            return false;

        foreach (var (material, volume) in composition.MaterialComposition)
        {
            _materialStorage.TryChangeMaterialAmount(silo, material, volume * multiplier, silo.Comp);
        }

        Del(item);
        return true;
    }
}

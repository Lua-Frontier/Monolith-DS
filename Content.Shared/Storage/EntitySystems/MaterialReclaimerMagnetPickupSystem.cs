using Content.Server.Storage.Components;
using Content.Shared.Materials;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using Content.Shared.Whitelist; // Mono
using Content.Shared.Examine;   // Frontier
using Content.Shared.Hands.Components;  // Frontier
using Content.Shared.Verbs;     // Frontier
using Robust.Shared.Utility;    // Frontier
// LuaM-start
using Robust.Shared.Network;
using Robust.Shared.Random;
// LuaM-end

namespace Content.Shared.Storage.EntitySystems;

/// <summary>
/// <see cref="MaterialReclaimerMagnetPickupComponent"/>
/// </summary>
public sealed partial class MaterialReclaimerMagnetPickupSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMaterialReclaimerSystem _storage = default!;
    // LuaM-start
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    // LuaM-end

    private static readonly TimeSpan ScanDelay = TimeSpan.FromSeconds(1);
    private const int MaxEntitiesToInsert = 15; // Mono
    private readonly HashSet<EntityUid> _nearby = new(); // LuaM
    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        base.Initialize();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        SubscribeLocalEvent<MaterialReclaimerMagnetPickupComponent, MapInitEvent>(OnMagnetMapInit);
        SubscribeLocalEvent<MaterialReclaimerMagnetPickupComponent, ExaminedEvent>(OnExamined);  // Frontier
        SubscribeLocalEvent<MaterialReclaimerMagnetPickupComponent, GetVerbsEvent<AlternativeVerb>>(AddToggleMagnetVerb);    // Frontier
    }


    private void OnMagnetMapInit(EntityUid uid, MaterialReclaimerMagnetPickupComponent component, MapInitEvent args)
    {
        component.NextScan = _timing.CurTime + _random.NextFloat() * ScanDelay; // LuaM: + _random.NextFloat() * ScanDelay
    }

    // Frontier, used to add the magnet toggle to the context menu
    private void AddToggleMagnetVerb(EntityUid uid, MaterialReclaimerMagnetPickupComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!HasComp<HandsComponent>(args.User))
            return;

        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                ToggleMagnet(uid, component);
            },
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/Spare/poweronoff.svg.192dpi.png")),
            Text = Loc.GetString("magnet-pickup-component-toggle-verb"),
            Priority = 3
        };

        args.Verbs.Add(verb);
    }

    // Frontier, used to show the magnet state on examination
    private void OnExamined(EntityUid uid, MaterialReclaimerMagnetPickupComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("magnet-pickup-component-on-examine-main",
                        ("stateText", Loc.GetString(component.MagnetEnabled
                        ? "magnet-pickup-component-magnet-on"
                        : "magnet-pickup-component-magnet-off"))));
    }

    // Frontier, used to toggle the magnet on the ore bag/box
    public bool ToggleMagnet(EntityUid uid, MaterialReclaimerMagnetPickupComponent comp)
    {
        comp.MagnetEnabled = !comp.MagnetEnabled;

        return comp.MagnetEnabled;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient) // LuaM
            return; // LuaM

        var query = EntityQueryEnumerator<MaterialReclaimerMagnetPickupComponent, MaterialReclaimerComponent, TransformComponent>();
        var currentTime = _timing.CurTime;

        while (query.MoveNext(out var uid, out var comp, out var storage, out var xform))
        {
            if (comp.NextScan > currentTime) // Reversed - Mono
                continue;

            comp.NextScan = currentTime + ScanDelay; // Mono: no need to rerun if built late in-round

            // Frontier - magnet disabled
            if (!comp.MagnetEnabled || !_storage.CanStart(uid, storage)) // LuaM: || !_storage.CanStart(uid, storage)
                continue;

            var parentUid = xform.ParentUid;
            var count = 0; // Mono

            // LuaM-start
            _nearby.Clear();
            _lookup.GetEntitiesInRange(uid, comp.Range, _nearby, LookupFlags.Dynamic | LookupFlags.Sundries);

            foreach (var near in _nearby)
            // LuaM-end
            {
                if (count >= MaxEntitiesToInsert) // Mono
                    break;

                if (near == parentUid)
                    continue;

                if (!_physicsQuery.TryGetComponent(near, out var physics) || physics.BodyStatus != BodyStatus.OnGround)
                    continue;

                if (!_storage.TryStartProcessItem(uid, near))
                    continue;

                count++; // Mono

                if (!_storage.CanStart(uid, storage)) // LuaM
                    break; // LuaM
            }
        }
    }
}

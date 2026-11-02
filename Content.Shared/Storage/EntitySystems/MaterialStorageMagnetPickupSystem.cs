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
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;
// LuaM-end

namespace Content.Shared.Storage.EntitySystems;

/// <summary>
/// <see cref="MaterialStorageMagnetPickupComponent"/>
/// </summary>
public sealed partial class MaterialStorageMagnetPickupSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMaterialStorageSystem _storage = default!;
    // LuaM-start
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    // LuaM-end

    private static readonly TimeSpan ScanDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan IdleScanDelay = TimeSpan.FromSeconds(1.5); // LuaM
    private const int MaxEntitiesToInsert = 15; // Mono

    private readonly HashSet<Entity<MaterialComponent>> _nearby = new(); // LuaM
    private readonly HashSet<string> _popups = new(); // LuaM
    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        base.Initialize();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        SubscribeLocalEvent<MaterialStorageMagnetPickupComponent, MapInitEvent>(OnMagnetMapInit);
        SubscribeLocalEvent<MaterialStorageMagnetPickupComponent, ExaminedEvent>(OnExamined);  // Frontier
        SubscribeLocalEvent<MaterialStorageMagnetPickupComponent, GetVerbsEvent<AlternativeVerb>>(AddToggleMagnetVerb);    // Frontier
    }


    private void OnMagnetMapInit(EntityUid uid, MaterialStorageMagnetPickupComponent component, MapInitEvent args)
    {
        component.NextScan = _timing.CurTime + _random.NextFloat() * ScanDelay; // LuaM: + _random.NextFloat() * ScanDelay
    }

    // Frontier, used to add the magnet toggle to the context menu
    private void AddToggleMagnetVerb(EntityUid uid, MaterialStorageMagnetPickupComponent component, GetVerbsEvent<AlternativeVerb> args)
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
    private void OnExamined(EntityUid uid, MaterialStorageMagnetPickupComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("magnet-pickup-component-on-examine-main",
                        ("stateText", Loc.GetString(component.MagnetEnabled
                        ? "magnet-pickup-component-magnet-on"
                        : "magnet-pickup-component-magnet-off"))));
    }

    // Frontier, used to toggle the magnet on the ore bag/box
    public bool ToggleMagnet(EntityUid uid, MaterialStorageMagnetPickupComponent comp)
    {
        comp.MagnetEnabled = !comp.MagnetEnabled;

        if (comp.MagnetEnabled) // LuaM
            comp.NextScan = _timing.CurTime; // LuaM

        return comp.MagnetEnabled;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient) // LuaM
            return; // LuaM

        var query = EntityQueryEnumerator<MaterialStorageMagnetPickupComponent, MaterialStorageComponent, TransformComponent>();
        var currentTime = _timing.CurTime;

        while (query.MoveNext(out var uid, out var comp, out var storage, out var xform))
        {
            if (comp.NextScan > currentTime) // Reversed - Mono
                continue;

            comp.NextScan = currentTime + IdleScanDelay; // Mono: no need to rerun if built late in-round // LuaM: ScanDelay > IdleScanDelay

            // Frontier - magnet disabled
            if (!comp.MagnetEnabled || !_power.IsPowered(uid)) // LuaM: || !_power.IsPowered(uid)
                continue;

            var parentUid = xform.ParentUid;
            var count = 0; // Mono

            // LuaM-start: reused set, only entities that can be inserted at all
            _nearby.Clear();
            _popups.Clear();
            _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(uid, xform), comp.Range, _nearby, LookupFlags.Dynamic | LookupFlags.Sundries);

            foreach (var (near, material) in _nearby)
            // LuaM-end
            {
                if (count >= MaxEntitiesToInsert) // Mono
                    break;

                if (near == uid || near == parentUid) // LuaM: near == uid ||
                    continue;

                if (!_physicsQuery.TryGetComponent(near, out var physics) || physics.BodyStatus != BodyStatus.OnGround)
                    continue;

                var popup = Loc.GetString("machine-insert-item", ("user", uid), ("machine", uid), ("item", near)); // LuaM

                if (!_storage.TryInsertMaterialEntity(uid, near, uid, storage, material)) // LuaM: + material
                    continue;

                // LuaM-start: one popup per kind of item in a scan
                if (_popups.Add(popup))
                    _popup.PopupEntity(popup, uid);
                // LuaM-end

                count++; // Mono
            }

            // LuaM-start
            if (count == 0)
                continue;

            comp.NextScan = currentTime + ScanDelay;
            _audio.PlayPvs(storage.InsertingSound, uid);
            // LuaM-end
        }
    }
}

using Content.Server.Popups;
using Content.Server.Research.Disk;
using Content.Server.Research.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Research.Components;
using Content.Shared.Verbs;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._LuaM.Research;

/// <summary>
/// Handles <see cref="ResearchServerMagnetComponent"/>: research disks on the floor near the server get absorbed into it.
/// </summary>
public sealed class ResearchServerMagnetSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private PopupSystem _popup = default!;

    private const int MaxDisksPerScan = 15;

    private EntityQuery<ResearchDiskComponent> _diskQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        base.Initialize();

        _diskQuery = GetEntityQuery<ResearchDiskComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();

        SubscribeLocalEvent<ResearchServerMagnetComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ResearchServerMagnetComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ResearchServerMagnetComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnMapInit(Entity<ResearchServerMagnetComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextScan = _timing.CurTime;
    }

    private void OnExamined(Entity<ResearchServerMagnetComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("magnet-pickup-component-on-examine-main",
            ("stateText", Loc.GetString(ent.Comp.MagnetEnabled
                ? "magnet-pickup-component-magnet-on"
                : "magnet-pickup-component-magnet-off"))));
    }

    private void OnGetVerbs(Entity<ResearchServerMagnetComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<HandsComponent>(args.User))
            return;

        var comp = ent.Comp;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => comp.MagnetEnabled = !comp.MagnetEnabled,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/Spare/poweronoff.svg.192dpi.png")),
            Text = Loc.GetString("magnet-pickup-component-toggle-verb"),
            Priority = 3,
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ResearchServerMagnetComponent, ResearchServerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var magnet, out var server, out var xform))
        {
            if (magnet.NextScan > curTime)
                continue;

            magnet.NextScan = curTime + magnet.ScanDelay;

            if (!magnet.MagnetEnabled)
                continue;

            var count = 0;
            foreach (var near in _lookup.GetEntitiesInRange(uid, magnet.Range, LookupFlags.Dynamic | LookupFlags.Sundries))
            {
                if (count >= MaxDisksPerScan)
                    break;

                if (near == xform.ParentUid || !_diskQuery.TryComp(near, out var disk))
                    continue;

                // Only disks lying on the floor, not ones in flight or in someone's hands.
                if (!_physicsQuery.TryComp(near, out var physics) || physics.BodyStatus != BodyStatus.OnGround)
                    continue;

                _research.ModifyServerPoints(uid, disk.Points, server);
                _popup.PopupEntity(Loc.GetString("research-server-magnet-absorbed", ("points", disk.Points)), uid);
                QueueDel(near);
                count++;
            }
        }
    }
}

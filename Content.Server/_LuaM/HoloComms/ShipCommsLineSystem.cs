using System.Diagnostics.CodeAnalysis;
using Content.Server._LuaM.Shuttles;
using Content.Server.Holopad;
using Content.Server.Station.Systems;
using Content.Server.Telephone;
using Content.Shared._LuaM.HoloComms;
using Content.Shared.Holopad;
using Content.Shared.Labels.Components;
using Content.Shared.Telephone;

namespace Content.Server._LuaM.HoloComms;

/// <summary>
/// Joins the comms points of a ship (the shuttle consoles and the ship holopads) into one line.
/// A call to the ship rings every point of it, once someone answers every point of both ships joins the call,
/// and hanging up anywhere hangs up the whole ship. Stations and POI holopads stand on their own.
/// </summary>
public sealed partial class ShipCommsLineSystem : EntitySystem
{
    [Dependency] private TelephoneSystem _telephone = default!;
    [Dependency] private HolopadSystem _holopad = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private RadarIdentificationSystem _identification = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private EntityQuery<ShipCommsLineComponent> _lineQuery;
    private EntityQuery<TelephoneComponent> _telephoneQuery;
    private EntityQuery<HolopadComponent> _holopadQuery;

    // Linking the points raises the same telephone events again, the outer handler already takes care of them
    private bool _linking;

    public override void Initialize()
    {
        base.Initialize();

        _lineQuery = GetEntityQuery<ShipCommsLineComponent>();
        _telephoneQuery = GetEntityQuery<TelephoneComponent>();
        _holopadQuery = GetEntityQuery<HolopadComponent>();

        SubscribeLocalEvent<ShipCommsLineComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShipCommsLineComponent, TelephoneStateChangeEvent>(OnStateChange);
        SubscribeLocalEvent<ShipCommsLineComponent, TelephoneCallCommencedEvent>(OnCallCommenced, after: [typeof(HolopadSystem)]);
        SubscribeLocalEvent<ShipCommsLineComponent, HolopadEndCallMessage>(OnHolopadEndCall, before: [typeof(HolopadSystem)]);
        SubscribeLocalEvent<ShipCommsLineComponent, ShipCommsSetDoNotDisturbMessage>(OnHolopadSetDoNotDisturb);
        SubscribeLocalEvent<TelephoneComponent, TelephoneCallAttemptEvent>(OnCallAttempt);
    }

    #region Events

    private void OnMapInit(Entity<ShipCommsLineComponent> ent, ref MapInitEvent args)
    {
        // A point built or bought later takes the setting the ship already has
        if (!TryGetShip(ent, out var grid))
            return;

        foreach (var point in GetPoints(grid.Value, ent))
        {
            var other = _lineQuery.Comp(point);
            if (other.DoNotDisturb != ent.Comp.DoNotDisturb)
            {
                ent.Comp.DoNotDisturb = other.DoNotDisturb;
                Dirty(ent);
            }

            break;
        }
    }

    private void OnStateChange(Entity<ShipCommsLineComponent> ent, ref TelephoneStateChangeEvent args)
    {
        if (_linking || args.NewState != TelephoneState.Ringing || !TryGetShip(ent, out var grid))
            return;

        // A ship is rung by one caller, broadcasts connect without ringing
        var telephone = _telephoneQuery.Comp(ent);
        if (telephone.LinkedTelephones.Count != 1)
            return;

        Entity<TelephoneComponent> caller = default;
        foreach (var linked in telephone.LinkedTelephones)
        {
            caller = linked;
        }

        // A call made inside the ship rings only the point it was made to
        if (Transform(caller).GridUid == grid)
            return;

        var user = GetCallUser(caller);

        _linking = true;
        try
        {
            foreach (var point in GetPoints(grid.Value, ent))
            {
                if (point.Comp.CurrentState == TelephoneState.Idle)
                    _telephone.TryAddTelephoneToCall(caller, point, user);
            }
        }
        finally
        {
            _linking = false;
        }
    }

    private void OnCallCommenced(Entity<ShipCommsLineComponent> ent, ref TelephoneCallCommencedEvent args)
    {
        if (_linking)
            return;

        var telephone = _telephoneQuery.Comp(ent);
        var remote = args.Receiver;

        // Broadcasts and the AI projector mute one side, those are not conversations between ships
        if (telephone.Muted || remote.Comp.Muted)
            return;

        TryGetShip(ent, out var localGrid);
        TryGetShip(remote, out var remoteGrid);

        if (localGrid == null || localGrid == Transform(remote).GridUid)
            return;

        var local = new Entity<TelephoneComponent>(ent, telephone);
        var localSide = GetJoinableSide(localGrid.Value, local, remote);
        var remoteSide = remoteGrid != null ? GetJoinableSide(remoteGrid.Value, remote, local) : [remote];

        if (localSide.Count == 1 && remoteSide.Count == 1)
            return;

        _linking = true;
        try
        {
            ConnectSides(localSide, remoteSide);
        }
        finally
        {
            _linking = false;
        }

        // Every joined point resets the holograms it is linked to, show the two who started the call again
        RefreshShownUser(remote);
        RefreshShownUser(local);
    }

    private void OnHolopadEndCall(Entity<ShipCommsLineComponent> ent, ref HolopadEndCallMessage args)
    {
        if (!_holopadQuery.TryComp(ent, out var holopad) ||
            _holopad.IsHolopadControlLocked((ent, holopad), args.Actor) ||
            !_telephoneQuery.TryComp(ent, out var telephone))
        {
            return;
        }

        // The holopad hangs up itself right after this
        HangUpOtherPoints((ent, telephone));
    }

    private void OnHolopadSetDoNotDisturb(Entity<ShipCommsLineComponent> ent, ref ShipCommsSetDoNotDisturbMessage args)
    {
        SetDoNotDisturb(ent, args.Enabled);
    }

    private void OnCallAttempt(Entity<TelephoneComponent> ent, ref TelephoneCallAttemptEvent args)
    {
        // The points of a call already going on still join each other
        if (_telephone.IsTelephoneEngaged(args.Receiver))
            return;

        if (!_lineQuery.TryComp(args.Receiver, out var line) || !line.Enabled || !line.DoNotDisturb)
            return;

        if (Transform(args.Receiver).GridUid == Transform(ent).GridUid)
            return;

        args.Cancelled = true;
    }

    #endregion

    #region Public API

    /// <summary>
    /// The grid of the ship the point belongs to, if it is a part of a ship line.
    /// </summary>
    public bool TryGetShip(EntityUid point, [NotNullWhen(true)] out EntityUid? grid)
    {
        grid = null;

        if (!_lineQuery.TryComp(point, out var line) || !line.Enabled)
            return false;

        grid = Transform(point).GridUid;
        return grid != null;
    }

    /// <summary>
    /// The points that talk for the same side as this one: every point of its ship, or only itself.
    /// </summary>
    public void GetSidePoints(EntityUid point, List<EntityUid> points)
    {
        if (TryGetShip(point, out var grid))
        {
            foreach (var shipPoint in GetPoints(grid.Value))
            {
                points.Add(shipPoint.Owner);
            }
        }

        if (!points.Contains(point))
            points.Add(point);
    }

    /// <summary>
    /// Ends the call on the point and on every other point of its ship that talks to the same side.
    /// </summary>
    public void HangUp(Entity<TelephoneComponent> point)
    {
        HangUpOtherPoints(point);
        _telephone.EndTelephoneCalls(point);
    }

    /// <summary>
    /// Another ship is listed once, the call goes to whichever of its points is free.
    /// Inside the own ship the call goes to the point that was picked.
    /// </summary>
    public EntityUid ResolveCallTarget(EntityUid source, EntityUid target)
    {
        if (!TryGetShip(target, out var grid) ||
            grid == Transform(source).GridUid ||
            !_telephoneQuery.TryComp(target, out var telephone))
        {
            return target;
        }

        if (IsFree((target, telephone)))
            return target;

        foreach (var point in GetPoints(grid.Value, target))
        {
            if (IsFree(point))
                return point;
        }

        return target;
    }

    public void SetDoNotDisturb(EntityUid point, bool enabled)
    {
        if (!TryGetShip(point, out var grid))
            return;

        var points = GetPoints(grid.Value);
        foreach (var other in points)
        {
            var line = _lineQuery.Comp(other);
            if (line.DoNotDisturb == enabled)
                continue;

            line.DoNotDisturb = enabled;
            Dirty(other, line);
        }

        // Turning it on declines the call that is ringing right now
        if (enabled)
        {
            foreach (var other in points)
            {
                if (other.Comp.CurrentState == TelephoneState.Ringing)
                {
                    HangUp(other);
                    break;
                }
            }
        }

        var ev = new ShipCommsLineChangedEvent(grid.Value);
        RaiseLocalEvent(ref ev);
    }

    public bool IsDoNotDisturb(EntityUid point)
    {
        return _lineQuery.TryComp(point, out var line) && line.Enabled && line.DoNotDisturb;
    }

    public bool CanCall(Entity<TelephoneComponent> source, Entity<TelephoneComponent> receiver, EntityUid? viewer = null)
    {
        if (receiver.Owner == source.Owner ||
            receiver.Comp.UnlistedNumber ||
            !_holopadQuery.HasComp(receiver) ||
            !_telephone.IsSourceInRangeOfReceiver(source, receiver))
        {
            return false;
        }

        var sourceGrid = Transform(source).GridUid;
        var receiverGrid = Transform(receiver).GridUid;

        // Inside the own ship the holopads are called one by one, the consoles only talk to other ships
        if (receiverGrid == sourceGrid && sourceGrid != null)
            return !HasComp<HoloCommsModuleComponent>(receiver);

        return receiverGrid != null &&
               _identification.IsIdentified(receiverGrid.Value, _xform.GetMapCoordinates(source), viewer ?? sourceGrid);
    }

    public void GetGridContacts(Entity<TelephoneComponent> source, EntityUid grid, EntityUid viewer, List<HoloCommsContact> contacts)
    {
        Entity<TelephoneComponent>? shipPoint = null;
        var shipFree = false;

        var query = EntityQueryEnumerator<HolopadComponent, TelephoneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var telephone, out var xform))
        {
            var receiver = new Entity<TelephoneComponent>(uid, telephone);
            if (xform.GridUid != grid || !CanCall(source, receiver, viewer))
                continue;

            var free = IsFree(receiver) && !IsDoNotDisturb(uid);

            if (TryGetShip(uid, out _))
            {
                if (shipPoint == null || free && !shipFree)
                {
                    shipPoint = receiver;
                    shipFree = free;
                }

                continue;
            }

            contacts.Add(new HoloCommsContact(GetNetEntity(uid), GetLabelOrName(uid), HoloCommsContactKind.Holopad, !free));
        }

        contacts.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

        if (shipPoint is { } point)
            contacts.Insert(0, new HoloCommsContact(GetNetEntity(point), GetShipName(point), HoloCommsContactKind.Ship, !shipFree));
    }

    /// <summary>
    /// Everybody the source can call. Other ships are listed once by their name, ships that take no calls are left out.
    /// </summary>
    public void GetContacts(Entity<TelephoneComponent> source, List<HoloCommsContact> contacts)
    {
        var ships = new Dictionary<EntityUid, (EntityUid Point, bool Free)>();
        var sourceGrid = Transform(source).GridUid;

        var query = EntityQueryEnumerator<HolopadComponent, TelephoneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var telephone, out var xform))
        {
            var receiver = new Entity<TelephoneComponent>(uid, telephone);
            if (!CanCall(source, receiver))
                continue;

            if (xform.GridUid == sourceGrid && sourceGrid != null)
            {
                contacts.Add(new HoloCommsContact(GetNetEntity(uid), GetLabelOrName(uid), HoloCommsContactKind.Holopad, _telephone.IsTelephoneEngaged(receiver)));
                continue;
            }

            if (_lineQuery.TryComp(uid, out var line) && line.Enabled && xform.GridUid is { } grid)
            {
                if (line.DoNotDisturb || !_telephone.IsTelephonePowered(receiver))
                    continue;

                var free = !_telephone.IsTelephoneEngaged(receiver);
                if (!ships.TryGetValue(grid, out var known) || free && !known.Free)
                    ships[grid] = (uid, free);

                continue;
            }

            contacts.Add(new HoloCommsContact(GetNetEntity(uid), GetPointName(uid), HoloCommsContactKind.Holopad, _telephone.IsTelephoneEngaged(receiver)));
        }

        foreach (var (point, free) in ships.Values)
        {
            contacts.Add(new HoloCommsContact(GetNetEntity(point), GetShipName(point), HoloCommsContactKind.Ship, !free));
        }

        contacts.Sort((a, b) => a.Kind != b.Kind
            ? a.Kind.CompareTo(b.Kind)
            : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>
    /// The name the point shows up under: its ship for a ship line, its label or name otherwise.
    /// </summary>
    public string GetRemoteName(EntityUid point, EntityUid remote)
    {
        var ownGrid = Transform(point).GridUid;
        if (Transform(remote).GridUid is { } remoteGrid &&
            remoteGrid != ownGrid &&
            !_identification.IsIdentified(remoteGrid, _xform.GetMapCoordinates(point), ownGrid))
        {
            return Loc.GetString("holo-comms-remote-unknown");
        }

        return GetPointName(remote);
    }

    public string GetPointName(EntityUid point)
    {
        return TryGetShip(point, out _) ? GetShipName(point) : GetLabelOrName(point);
    }

    private string GetLabelOrName(EntityUid point)
    {
        if (TryComp<LabelComponent>(point, out var label) && !string.IsNullOrEmpty(label.CurrentLabel))
            return label.CurrentLabel;

        return Name(point);
    }

    public string GetShipName(EntityUid point)
    {
        if (_station.GetOwningStation(point) is { } station)
            return Name(station);

        if (Transform(point).GridUid is { } grid)
            return Name(grid);

        return Name(point);
    }

    #endregion

    private void HangUpOtherPoints(Entity<TelephoneComponent> point)
    {
        if (!TryGetShip(point, out var grid) || point.Comp.LinkedTelephones.Count == 0)
            return;

        // Only the points talking to the same side, another call on the ship goes on
        var remote = GetLinks(point.Comp);
        foreach (var other in GetPoints(grid.Value, point))
        {
            if (SharesLink(other.Comp, remote))
                _telephone.EndTelephoneCalls(other);
        }
    }

    /// <summary>
    /// The points of a ship that can take part in a call with the other side:
    /// the free ones and the ones ringing because of it, never those busy with another call.
    /// </summary>
    private List<Entity<TelephoneComponent>> GetJoinableSide(EntityUid grid, Entity<TelephoneComponent> point, Entity<TelephoneComponent> other)
    {
        var side = new List<Entity<TelephoneComponent>> { point };
        var call = GetLinks(point.Comp);

        foreach (var candidate in GetPoints(grid, point))
        {
            if (!_telephone.IsTelephonePowered(candidate))
                continue;

            var telephone = candidate.Comp;
            var joinable = telephone.CurrentState switch
            {
                TelephoneState.Idle => true,
                TelephoneState.Ringing or TelephoneState.Calling or TelephoneState.InCall =>
                    IsLinkedTo(telephone, other) || SharesLink(telephone, call),
                _ => false,
            };

            if (joinable)
                side.Add(candidate);
        }

        return side;
    }

    /// <summary>
    /// Links every point of one side to every point of the other.
    /// A point only joins a call that is up, so the ones still ringing are connected first.
    /// </summary>
    private void ConnectSides(List<Entity<TelephoneComponent>> localSide, List<Entity<TelephoneComponent>> remoteSide)
    {
        var options = new TelephoneCallOptions { ForceJoin = true };
        bool progress;

        do
        {
            progress = false;

            foreach (var a in localSide)
            {
                foreach (var b in remoteSide)
                {
                    if (IsConnected(a, b))
                        continue;

                    if (b.Comp.CurrentState == TelephoneState.InCall)
                        _telephone.TryAddTelephoneToCall(a, b, GetCallUser(a), options);
                    else if (a.Comp.CurrentState == TelephoneState.InCall)
                        _telephone.TryAddTelephoneToCall(b, a, GetCallUser(b), options);
                    else
                        continue;

                    progress |= IsConnected(a, b);
                }
            }
        }
        while (progress);
    }

    private static bool IsConnected(Entity<TelephoneComponent> a, Entity<TelephoneComponent> b)
    {
        return a.Comp.CurrentState == TelephoneState.InCall &&
               b.Comp.CurrentState == TelephoneState.InCall &&
               IsLinkedTo(a.Comp, b);
    }

    // The telephone owns its links, they are only read here

    private static bool IsLinkedTo(TelephoneComponent telephone, EntityUid other)
    {
        foreach (var linked in telephone.LinkedTelephones)
        {
            if (linked.Owner == other)
                return true;
        }

        return false;
    }

    private static bool SharesLink(TelephoneComponent telephone, HashSet<EntityUid> others)
    {
        foreach (var linked in telephone.LinkedTelephones)
        {
            if (others.Contains(linked.Owner))
                return true;
        }

        return false;
    }

    private static HashSet<EntityUid> GetLinks(TelephoneComponent telephone)
    {
        var links = new HashSet<EntityUid>();
        foreach (var linked in telephone.LinkedTelephones)
        {
            links.Add(linked.Owner);
        }

        return links;
    }

    private void RefreshShownUser(EntityUid point)
    {
        if (_holopadQuery.TryComp(point, out var holopad))
            _holopad.SetHolopadUser((point, holopad), holopad.User?.Owner);
    }

    private bool IsFree(Entity<TelephoneComponent> point)
    {
        return !_telephone.IsTelephoneEngaged(point) && _telephone.IsTelephonePowered(point);
    }

    /// <summary>
    /// Who the call is placed in the name of, for the caller ID.
    /// </summary>
    private EntityUid GetCallUser(EntityUid point)
    {
        return _holopadQuery.TryComp(point, out var holopad) && holopad.User is { } user ? user.Owner : point;
    }

    private List<Entity<TelephoneComponent>> GetPoints(EntityUid grid, EntityUid? except = null)
    {
        var points = new List<Entity<TelephoneComponent>>();

        var query = EntityQueryEnumerator<ShipCommsLineComponent, TelephoneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var line, out var telephone, out var xform))
        {
            if (line.Enabled && xform.GridUid == grid && uid != except)
                points.Add((uid, telephone));
        }

        return points;
    }
}

/// <summary>
/// Raised when a ship changes how it takes calls.
/// </summary>
[ByRefEvent]
public readonly record struct ShipCommsLineChangedEvent(EntityUid Grid);

using System.Numerics;
using Content.Server._Mono.Projectiles.TargetGuided;
using Content.Server._Mono.Projectiles.TargetSeeking;
using Content.Shared._Mono.Radar;
using Content.Shared.Projectiles;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Enums; // LuaM
using Robust.Shared.Player; // LuaM
using Robust.Shared.Timing; // LuaM

namespace Content.Server._Mono.Radar;

public sealed partial class RadarBlipSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IGameTiming _timing = default!; // LuaM

    // LuaM-start: a blip removal only goes to the players whose radar is showing that blip, not to the whole server
    /// <summary>
    /// The client drops its blips after 3 seconds without an update (BlipStaleSeconds), so a viewer older than that has nothing to remove.
    /// </summary>
    private static readonly TimeSpan RadarViewerTimeout = TimeSpan.FromSeconds(3);

    private readonly Dictionary<ICommonSession, RadarViewer> _radarViewers = new();
    private readonly List<ICommonSession> _staleViewers = new();

    /// <summary>
    /// When the player last got a blip report and which blips it had. Each report replaces the previous one on the client.
    /// </summary>
    private sealed class RadarViewer
    {
        public TimeSpan LastReport;
        public readonly HashSet<NetEntity> Blips = new();
    }
    // LuaM-end

    // Pooled collections to avoid per-request heap churn
    private readonly List<BlipNetData> _tempBlipsCache = new();
    private readonly List<MissileVectorNetData> _tempMissileCache = new();
    private readonly List<HitscanNetData> _tempHitscansCache = new();
    private readonly List<EntityUid> _tempSourcesCache = new();
    private readonly List<BlipConfig> _tempPaletteCache = new();
    private readonly Dictionary<BlipConfig, ushort> _paletteIndex = new();
    private readonly List<Vector2> _tempSourcePositions = new(); // LuaM
    private readonly HashSet<EntityUid> _reportedBlips = new(); // LuaM

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestBlipsEvent>(OnBlipsRequested);
        SubscribeLocalEvent<RadarBlipComponent, ComponentShutdown>(OnBlipShutdown);
    }

    private void OnBlipsRequested(RequestBlipsEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetEntity(ev.Radar, out var radarUid)
            || !TryComp<RadarConsoleComponent>(radarUid, out var radar)
        )
            return;

        var sourcesEv = new GetRadarSourcesEvent();
        RaiseLocalEvent(radarUid.Value, ref sourcesEv);

        // Reuse pooled sources list
        _tempSourcesCache.Clear();
        if (sourcesEv.Sources != null)
            _tempSourcesCache.AddRange(sourcesEv.Sources);
        else
            _tempSourcesCache.Add(radarUid.Value);

        // LuaM-start: source positions are needed for every blip, look them up once per report
        _tempSourcePositions.Clear();
        foreach (var source in _tempSourcesCache)
        {
            _tempSourcePositions.Add(_xform.GetWorldPosition(source));
        }
        // LuaM-end

        CollectVeilFields(Transform(radarUid.Value).MapID); // Lua
        AssembleBlipsReport((EntityUid)radarUid, _tempSourcesCache, radar);
        AssembleHitscanReport((EntityUid)radarUid, _tempSourcesCache, radar);

        // Combine the blips and hitscan lines
        var giveEv = new GiveBlipsEvent(_tempPaletteCache, _tempBlipsCache, _tempMissileCache, _tempHitscansCache);
        RaiseNetworkEvent(giveEv, args.SenderSession);

        // LuaM-start: remember what this player's radar shows now
        if (!_radarViewers.TryGetValue(args.SenderSession, out var viewer))
        {
            viewer = new RadarViewer();
            _radarViewers[args.SenderSession] = viewer;
        }

        viewer.LastReport = _timing.CurTime;
        viewer.Blips.Clear();
        foreach (var blip in _tempBlipsCache)
        {
            viewer.Blips.Add(blip.Uid);
        }
        // LuaM-end

        _tempBlipsCache.Clear();
        _tempMissileCache.Clear();
        _tempHitscansCache.Clear();
        _reportedBlips.Clear(); // LuaM
        _tempSourcesCache.Clear();
        _tempPaletteCache.Clear();
        _paletteIndex.Clear();
    }

    private void OnBlipShutdown(EntityUid blipUid, RadarBlipComponent component, ComponentShutdown args)
    {
        // LuaM-start: was sent to every player on the server for every deleted shell and missile
        if (_radarViewers.Count == 0)
            return;

        var netBlipUid = GetNetEntity(blipUid);
        var now = _timing.CurTime;
        Filter? viewers = null;
        foreach (var (session, viewer) in _radarViewers)
        {
            if (now - viewer.LastReport > RadarViewerTimeout || session.Status == SessionStatus.Disconnected)
            {
                _staleViewers.Add(session);
                continue;
            }

            // their radar never showed it, there is nothing to remove
            if (!viewer.Blips.Remove(netBlipUid))
                continue;

            viewers ??= Filter.Empty();
            viewers.AddPlayer(session);
        }

        foreach (var session in _staleViewers)
        {
            _radarViewers.Remove(session);
        }

        _staleViewers.Clear();

        if (viewers == null)
            return;

        var removalEv = new BlipRemovalEvent(netBlipUid);
        RaiseNetworkEvent(removalEv, viewers);
        // LuaM-end
    }

    private void AssembleBlipsReport(EntityUid uid, List<EntityUid> sources, RadarConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var radarXform = Transform(uid);
        var radarGrid = radarXform.GridUid;
        var radarMapId = radarXform.MapID;

        var blipQuery = EntityQueryEnumerator<RadarBlipComponent, TransformComponent, PhysicsComponent>();

        while (blipQuery.MoveNext(out var blipUid, out var blip, out var blipXform, out var blipPhysics))
        {
            // LuaM-start: cheap field checks go before the world position lookup, which is done once instead of twice
            if (!blip.Enabled || blipXform.MapID != radarMapId)
                continue;

            var blipGrid = blipXform.GridUid;

            if (blip.RequireNoGrid && blipGrid != null // if we want no grid but we are on a grid
                || !blip.VisibleFromOtherGrids && blipGrid != radarGrid // or if we don't want to be visible from other grids but we're on another grid
            )
                continue; // don't show this blip

            var blipWorldPos = _xform.GetWorldPosition(blipXform);
            if (!NearAnySources(blipWorldPos, _tempSourcePositions, blip.MaxDistance))
                continue;

            if (blipGrid == null && _veilFields.Count > 0 && IsInNebulaVeil(blipWorldPos)) // Lua: a veil nebula hides what flies inside it
                continue;

            _reportedBlips.Add(blipUid);
            // LuaM-end

            var netBlipUid = GetNetEntity(blipUid);

            var blipVelocity = _physics.GetMapLinearVelocity(blipUid, blipPhysics, blipXform);

            // due to PVS being a thing, things will break if we try to parent to not the map or a grid
            var coord = blipXform.Coordinates;
            if (blipXform.ParentUid != blipXform.MapUid && blipXform.ParentUid != blipGrid)
                coord = _xform.WithEntityId(coord, blipGrid ?? blipXform.MapUid!.Value);

            var gridCfg = (BlipConfig?)null;
            var rotation = _xform.GetWorldRotation(blipXform);

            // we're parented to either the map or a grid and this is relative velocity so account for grid movement
            if (blipGrid != null)
            {
                var gridXform = Transform(blipGrid.Value);
                if (TryComp<PhysicsComponent>(blipGrid.Value, out var gridBody)) // prevent log spam
                    blipVelocity -= _physics.GetLinearVelocity(blipGrid.Value, coord.Position, gridBody);
                // it's local-frame velocity so rotate it too
                blipVelocity = (-gridXform.LocalRotation).RotateVec(blipVelocity);
                // and also offset the rotation
                rotation -= gridXform.LocalRotation;
                // and hijack our shape if we want to
                gridCfg = blip.GridConfig;
            }

            var configIdx = GetOrAddConfig(blip.Config);
            ushort? gridConfigIdx = gridCfg is { } gridCf ? GetOrAddConfig(gridCf) : null;

            // ideally we would handle blips being culled by detection on server but detection grid culling is already clientside so might as well
            _tempBlipsCache.Add(new(netBlipUid,
                            GetNetCoordinates(coord),
                            blipVelocity,
                            rotation,
                            configIdx,
                            gridConfigIdx));
        }

        var missileQuery = EntityQueryEnumerator<TargetSeekingComponent, RadarBlipComponent, TransformComponent>();
        while (missileQuery.MoveNext(out var missile, out var seeker, out var missileBlip, out var missileBlipXform))
        {
            // LuaM: only missiles whose blip is in this report, the lines of the rest were sent from the whole server and drawn from the origin
            if (!seeker.ArcLines || !_reportedBlips.Contains(missile))
                continue;

            var netMissileUid = GetNetEntity(missile);
            var missileArc = MathHelper.DegreesToRadians(seeker.ScanArc);
            if (seeker.ArcLines)
                _tempMissileCache.Add(new(netMissileUid,
                    (float)(seeker.MaxSpeed * 0.2),
                    missileArc));
        }
        // Check for SACLOS/mouse-guided missiles to add their directional lines to the console.
        var saclosQuery = EntityQueryEnumerator<TargetGuidedComponent, RadarBlipComponent, TransformComponent>();
        while (saclosQuery.MoveNext(out var missile, out var seeker, out var missileBlip, out var missileBlipXform))
        {
            if (!seeker.RadarLines || !_reportedBlips.Contains(missile)) // LuaM
                continue;

            var netMissileUid = GetNetEntity(missile);
            if (seeker.RadarLines)
                _tempMissileCache.Add(new(netMissileUid,
                    (float)(seeker.CurrentSpeed * 0.2),
                    0));
        }
    }

    /// <summary>
    /// Gets or create palette index for blip config.
    /// </summary>
    private ushort GetOrAddConfig(BlipConfig config)
    {
        if (_paletteIndex.TryGetValue(config, out var index))
            return index;

        if (_tempPaletteCache.Count >= ushort.MaxValue)
        {
            Log.Error($"Blip config count overflow! Reached max {ushort.MaxValue}, but trying to add more.");
            return 0;
        }

        index = (ushort)_tempPaletteCache.Count;
        _tempPaletteCache.Add(config);
        _paletteIndex[config] = index;
        return index;
    }

    /// <summary>
    /// Assembles trajectory information for hitscan projectiles to be displayed on radar
    /// </summary>
    private void AssembleHitscanReport(EntityUid uid, List<EntityUid> sources, RadarConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var radarXform = Transform(uid);

        var hitscanQuery = EntityQueryEnumerator<HitscanRadarComponent>();

        while (hitscanQuery.MoveNext(out var hitscanUid, out var hitscan))
        {
            if (!hitscan.Enabled)
                continue;

            // LuaM: skip only when both ends are out of range (the old check sent far-away shots and dropped ones fired into range from outside)
            if (!NearAnySources(hitscan.StartPosition, _tempSourcePositions, component.MaxRange) && !NearAnySources(hitscan.EndPosition, _tempSourcePositions, component.MaxRange))
                continue;

            if (IsInNebulaVeil(hitscan.StartPosition) || IsInNebulaVeil(hitscan.EndPosition)) // Lua
                continue;

            _tempHitscansCache.Add(new(hitscan.StartPosition, hitscan.EndPosition, hitscan.LineThickness, hitscan.RadarColor));
        }
    }

    private static bool NearAnySources(Vector2 coord, List<Vector2> sources, float range) // LuaM: positions instead of entities
    {
        var rsqr = range * range;
        foreach (var pos in sources) // LuaM
        {
            if ((pos - coord).LengthSquared() < rsqr)
                return true;
        }
        return false;
    }
}

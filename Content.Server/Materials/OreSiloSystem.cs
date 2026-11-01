using System.Numerics; // LuaM
using Content.Server.Pinpointer;
using Content.Shared.IdentityManagement;
using Content.Shared.Materials.OreSilo;
using Robust.Server.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Utility; // LuaM

namespace Content.Server.Materials;

/// <inheritdoc/>
public sealed partial class OreSiloSystem : SharedOreSiloSystem
{
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private SharedUserInterfaceSystem _userInterface = default!;

    private const float OreSiloPreloadRangeSquared = 225f; // ~1 screen

    private readonly HashSet<Entity<OreSiloClientComponent>> _clientLookup = new();
    private readonly HashSet<(NetEntity, string, string)> _clientInformation = new();
    private readonly HashSet<EntityUid> _silosToAdd = new();

    // LuaM-start
    private readonly Dictionary<EntityUid, List<(EntityUid Silo, Vector2 Position)>> _clientsByGrid = new();
    private readonly Dictionary<ICommonSession, HashSet<EntityUid>> _overriddenSilos = new();
    // LuaM-end

    // Mono
    private float _updateAccumulator = 0f;
    private float _updateInterval = 1f;

    protected override void UpdateOreSiloUi(Entity<OreSiloComponent> ent)
    {
        if (!_userInterface.IsUiOpen(ent.Owner, OreSiloUiKey.Key))
            return;
        _clientLookup.Clear();
        _clientInformation.Clear();

        var xform = Transform(ent);

        // Sneakily uses override with TComponent parameter
        _entityLookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.Range, _clientLookup);

        foreach (var client in _clientLookup)
        {
            // don't show already-linked clients.
            if (client.Comp.Silo is not null)
                continue;

            // Don't show clients on the screen if we can't link them.
            if (!CanTransmitMaterials((ent, ent, xform), client))
                continue;

            var netEnt = GetNetEntity(client);
            var name = Identity.Name(client, EntityManager);
            var beacon = _navMap.GetNearestBeaconString(client.Owner, onlyName: true);
            var inRange = CanTransmitMaterials((ent, ent, xform), client);

            var txt = Loc.GetString("ore-silo-ui-itemlist-entry",
                ("name", name),
                ("beacon", beacon),
                ("linked", ent.Comp.Clients.Contains(client)),
                ("inRange", true));

            _clientInformation.Add((netEnt, txt, beacon));
        }

        // Get all clients of this silo, including those out of range.
        foreach (var client in ent.Comp.Clients)
        {
            var netEnt = GetNetEntity(client);
            var name = Identity.Name(client, EntityManager);
            var beacon = _navMap.GetNearestBeaconString(client, onlyName: true);
            var inRange = CanTransmitMaterials((ent, ent), client);

            var txt = Loc.GetString("ore-silo-ui-itemlist-entry",
                ("name", name),
                ("beacon", beacon),
                ("linked", ent.Comp.Clients.Contains(client)),
                ("inRange", inRange));

            _clientInformation.Add((netEnt, txt, beacon));
        }

        _userInterface.SetUiState(ent.Owner, OreSiloUiKey.Key, new OreSiloBuiState(_clientInformation));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateAccumulator += frameTime;
        if (_updateAccumulator < _updateInterval)
            return;
        _updateAccumulator -= _updateInterval;

        // Solving an annoying problem: we need to send the silo to people who are near the silo so that
        // Things don't start wildly mispredicting. We do this as cheaply as possible via grid-based local-pos checks.
        // Sloth okay-ed this in the interim until a better solution comes around.

        // LuaM-start: grouped by grid instead of players * clients, overrides are removed when a player leaves the grid
        _clientsByGrid.Clear();
        var clientQuery = EntityQueryEnumerator<OreSiloClientComponent, TransformComponent>();
        while (clientQuery.MoveNext(out var clientComp, out var clientXform))
        {
            if (clientComp.Silo is not { } silo || TerminatingOrDeleted(silo) || clientXform.GridUid is not { } grid)
                continue;

            _clientsByGrid.GetOrNew(grid).Add((silo, clientXform.LocalPosition));
        }

        var actorQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actorQuery.MoveNext(out var actorComp, out var actorXform))
        {
            _silosToAdd.Clear();

            // We limit it to same-grid checks only for peak perf
            if (actorXform.GridUid is { } grid && _clientsByGrid.TryGetValue(grid, out var clients))
            {
                foreach (var (silo, position) in clients)
                {
                    if ((actorXform.LocalPosition - position).LengthSquared() <= OreSiloPreloadRangeSquared)
                        _silosToAdd.Add(silo);
                }
            }

            var session = actorComp.PlayerSession;
            if (_overriddenSilos.TryGetValue(session, out var overridden))
            {
                foreach (var silo in overridden)
                {
                    if (!_silosToAdd.Contains(silo))
                        _pvsOverride.RemoveSessionOverride(silo, session);
                }

                overridden.Clear();
            }

            if (_silosToAdd.Count == 0)
            {
                _overriddenSilos.Remove(session);
                continue;
            }

            overridden ??= _overriddenSilos[session] = new();
            foreach (var silo in _silosToAdd)
            {
                _pvsOverride.AddSessionOverride(silo, session);
                overridden.Add(silo);
            }
        }

        foreach (var (session, overridden) in _overriddenSilos)
        {
            if (session.AttachedEntity != null)
                continue;

            foreach (var silo in overridden)
            {
                _pvsOverride.RemoveSessionOverride(silo, session);
            }

            _overriddenSilos.Remove(session);
        }
        // LuaM-end
    }
}

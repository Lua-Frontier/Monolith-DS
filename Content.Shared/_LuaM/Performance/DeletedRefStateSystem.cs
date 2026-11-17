using Content.Shared._Mono.Shipyard;
using Content.Shared.Projectiles;
using Content.Shared.Station.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Performance;

// -- Автогенерированное состояние пишет ошибку со стектрейсом на каждую удалённую ссылку при каждой отправке в PVS-потоках.
public sealed class DeletedRefStateSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileComponent, ComponentGetState>(OnProjectileGetState);
        SubscribeLocalEvent<ProjectileComponent, ComponentHandleState>(OnProjectileHandleState);
        SubscribeLocalEvent<TargetedProjectileComponent, ComponentGetState>(OnTargetedGetState);
        SubscribeLocalEvent<TargetedProjectileComponent, ComponentHandleState>(OnTargetedHandleState);
        SubscribeLocalEvent<ShipGuestAccessComponent, ComponentGetState>(OnGuestGetState);
        SubscribeLocalEvent<ShipGuestAccessComponent, ComponentHandleState>(OnGuestHandleState);
        SubscribeLocalEvent<StationDataComponent, ComponentGetState>(OnStationGetState);
        SubscribeLocalEvent<StationDataComponent, ComponentHandleState>(OnStationHandleState);
    }

    private void OnProjectileGetState(Entity<ProjectileComponent> ent, ref ComponentGetState args)
    {
        args.State = new LuaMProjectileState(ent.Comp.Angle,
            Live(ent.Comp.Shooter),
            Live(ent.Comp.Weapon),
            ent.Comp.IgnoreShooter,
            ent.Comp.ProjectileSpent);
    }

    private void OnProjectileHandleState(Entity<ProjectileComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not LuaMProjectileState state)
            return;

        ent.Comp.Angle = state.Angle;
        ent.Comp.Shooter = EnsureEntity<ProjectileComponent>(state.Shooter, ent);
        ent.Comp.Weapon = EnsureEntity<ProjectileComponent>(state.Weapon, ent);
        ent.Comp.IgnoreShooter = state.IgnoreShooter;
        ent.Comp.ProjectileSpent = state.ProjectileSpent;
    }

    private void OnTargetedGetState(Entity<TargetedProjectileComponent> ent, ref ComponentGetState args)
    {
        args.State = new LuaMTargetedProjectileState(Live(ent.Comp.Target) ?? NetEntity.Invalid);
    }

    private void OnTargetedHandleState(Entity<TargetedProjectileComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is LuaMTargetedProjectileState state)
            ent.Comp.Target = EnsureEntity<TargetedProjectileComponent>(state.Target, ent);
    }

    private void OnGuestGetState(Entity<ShipGuestAccessComponent> ent, ref ComponentGetState args)
    {
        args.State = new LuaMShipGuestAccessState(LiveSet(ent.Comp.GuestIdCards), LiveSet(ent.Comp.GuestCyborgs));
    }

    private void OnGuestHandleState(Entity<ShipGuestAccessComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not LuaMShipGuestAccessState state)
            return;

        EnsureEntitySet<ShipGuestAccessComponent>(state.IdCards, ent, ent.Comp.GuestIdCards);
        EnsureEntitySet<ShipGuestAccessComponent>(state.Cyborgs, ent, ent.Comp.GuestCyborgs);
    }

    private void OnStationGetState(Entity<StationDataComponent> ent, ref ComponentGetState args)
    {
        args.State = new LuaMStationDataState(LiveSet(ent.Comp.Grids));
    }

    private void OnStationHandleState(Entity<StationDataComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is LuaMStationDataState state)
            EnsureEntitySet<StationDataComponent>(state.Grids, ent, ent.Comp.Grids);
    }

    private NetEntity? Live(EntityUid? uid)
    {
        if (uid == null)
            return null;

        return TryGetNetEntity(uid, out var net) ? net : NetEntity.Invalid;
    }

    private HashSet<NetEntity> LiveSet(HashSet<EntityUid> set)
    {
        var live = new HashSet<NetEntity>(set.Count);
        foreach (var uid in set)
        {
            if (TryGetNetEntity(uid, out var net))
                live.Add(net.Value);
        }

        return live;
    }
}

[Serializable, NetSerializable]
public sealed class LuaMProjectileState(Angle angle, NetEntity? shooter, NetEntity? weapon, bool ignoreShooter, bool projectileSpent) : ComponentState
{
    public readonly Angle Angle = angle;
    public readonly NetEntity? Shooter = shooter;
    public readonly NetEntity? Weapon = weapon;
    public readonly bool IgnoreShooter = ignoreShooter;
    public readonly bool ProjectileSpent = projectileSpent;
}

[Serializable, NetSerializable]
public sealed class LuaMTargetedProjectileState(NetEntity target) : ComponentState
{
    public readonly NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class LuaMShipGuestAccessState(HashSet<NetEntity> idCards, HashSet<NetEntity> cyborgs) : ComponentState
{
    public readonly HashSet<NetEntity> IdCards = idCards;
    public readonly HashSet<NetEntity> Cyborgs = cyborgs;
}

[Serializable, NetSerializable]
public sealed class LuaMStationDataState(HashSet<NetEntity> grids) : ComponentState
{
    public readonly HashSet<NetEntity> Grids = grids;
}

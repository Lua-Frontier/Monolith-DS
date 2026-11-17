using System.Diagnostics.CodeAnalysis;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;

namespace Content.Client._LuaM.StateRecovery;

public sealed partial class DetachLookupSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;

    private readonly Dictionary<EntityUid, BroadphaseComponent> _detachedFrom = new();
    private readonly List<EntityUid> _deleted = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EntParentChangedMessage>(OnParentChanged);
    }

    public override void Update(float frameTime)
    {
        _detachedFrom.Clear();
    }

    public int PurgeDeleted()
    {
        var removed = 0;
        var query = AllEntityQuery<BroadphaseComponent>();
        while (query.MoveNext(out var broadphase))
        {
            removed += PurgeDeleted(broadphase.SundriesTree);
            removed += PurgeDeleted(broadphase.StaticSundriesTree);
        }

        return removed;
    }

    private int PurgeDeleted(DynamicTree<EntityUid> tree)
    {
        _deleted.Clear();
        foreach (var uid in tree)
        {
            if (!Exists(uid))
                _deleted.Add(uid);
        }

        foreach (var uid in _deleted)
        {
            tree.Remove(uid);
        }

        return _deleted.Count;
    }

    private void OnParentChanged(ref EntParentChangedMessage args)
    {
        if (args.Transform.ParentUid.IsValid() ||
            args.OldParent is not { } oldParent ||
            !oldParent.IsValid() ||
            (MetaData(args.Entity).Flags & MetaDataFlags.Detached) == 0 ||
            !TryFindOldBroadphase(oldParent, out var broadphase))
        {
            return;
        }

        _detachedFrom[args.Entity] = broadphase;
        broadphase.SundriesTree.Remove(args.Entity);
        broadphase.StaticSundriesTree.Remove(args.Entity);
    }

    private bool TryFindOldBroadphase(EntityUid parent, [NotNullWhen(true)] out BroadphaseComponent? broadphase)
    {
        if (TryComp(parent, out broadphase) || _detachedFrom.TryGetValue(parent, out broadphase))
            return true;

        return TryComp(parent, out TransformComponent? parentXform) &&
               _lookup.TryFindBroadphase(parentXform, out broadphase);
    }
}

using Content.Shared.IgnitionSource;

namespace Content.Server.IgnitionSource;

public sealed partial class IgnitionSourceSystem
{
    private readonly HashSet<EntityUid> _lit = new();
    private readonly List<EntityUid> _litWalk = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IgnitionSourceComponent, ComponentStartup>(OnLitStartup);
        SubscribeLocalEvent<IgnitionSourceComponent, ComponentShutdown>(OnLitShutdown);
    }

    private void OnLitStartup(Entity<IgnitionSourceComponent> ent, ref ComponentStartup args)
    {
        IgnitedChanged(ent, ent.Comp.Ignited);
    }

    private void OnLitShutdown(Entity<IgnitionSourceComponent> ent, ref ComponentShutdown args)
    {
        _lit.Remove(ent);
    }

    protected override void IgnitedChanged(EntityUid uid, bool ignited)
    {
        if (ignited)
            _lit.Add(uid);
        else
            _lit.Remove(uid);
    }
}

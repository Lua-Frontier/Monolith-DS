using Content.Client.Shuttles.UI;
using Content.Shared._LuaM.Shuttles;

namespace Content.Client._LuaM.Shuttles;

/// <summary>
/// Radar grid geometry shared by every radar control.
/// Each console window used to rebuild every grid around when it opened, which dropped frames.
/// </summary>
public sealed class RadarGridCacheSystem : EntitySystem
{
    public readonly Dictionary<EntityUid, GridDrawData> GridData = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoved);
        SubscribeLocalEvent<RadarGridEdgesComponent, AfterAutoHandleStateEvent>(OnEdgesState);
        SubscribeLocalEvent<RadarGridEdgesComponent, ComponentShutdown>(OnEdgesShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        GridData.Clear();
    }

    private void OnGridRemoved(GridRemovalEvent ev)
    {
        GridData.Remove(ev.EntityUid);
    }

    private void OnEdgesState(Entity<RadarGridEdgesComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        GridData.Remove(ent);
    }

    private void OnEdgesShutdown(Entity<RadarGridEdgesComponent> ent, ref ComponentShutdown args)
    {
        GridData.Remove(ent);
    }
}

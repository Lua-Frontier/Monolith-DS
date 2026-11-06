using Content.Shared._LuaM.Atmos;
using Content.Shared._LuaM.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Atmos;

public sealed class SpaceWindVisualSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly EntProtoId VisualProto = "SpaceWindVisual";

    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SpaceWindVisualEvent>(OnSpaceWind);
        Subs.CVar(_cfg, LuaMCVars.SpaceWindVisuals, value => _enabled = value, true);
    }

    private void OnSpaceWind(SpaceWindVisualEvent ev)
    {
        if (!_enabled)
            return;

        var coordinates = GetCoordinates(ev.Coordinates);
        if (!coordinates.IsValid(EntityManager))
            return;

        var visual = Spawn(VisualProto, coordinates);
        _transform.SetLocalRotation(visual, ev.Rotation);
    }
}

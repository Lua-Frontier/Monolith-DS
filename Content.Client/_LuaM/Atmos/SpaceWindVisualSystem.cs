using Content.Shared._LuaM.Atmos;
using Content.Shared._LuaM.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Atmos;

public sealed partial class SpaceWindVisualSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

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

        // -- Угол сразу при создании: SetLocalRotation на клиенте интерполируется и для клиентской сущности застревает на полпути
        SpawnAttachedTo(VisualProto, coordinates, rotation: ev.Rotation);
    }
}

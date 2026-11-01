using Content.Shared._Starfall.Particles;
using Robust.Shared.Map;

namespace Content.Client._Starfall.Particles;

/// <summary>
/// Spawns a particle effect on this client when an entity with
/// <see cref="ParticleEmitterComponent"/> enters the local view (ComponentStartup).
/// </summary>
public sealed partial class ParticleEmitterSystem : EntitySystem
{
    [Dependency] private ParticleSystem _particles = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        // LuaM: MapInitEvent > ComponentStartup, the client does not raise map init for entities created from server state
        SubscribeLocalEvent<ParticleEmitterComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, ParticleEmitterComponent comp, ComponentStartup args)
    {
        var coords = _transform.GetMapCoordinates(uid);
        var emitter = _particles.SpawnEffect(comp.Effect, coords, uid, comp.ColorOverride);
        if (emitter == null)
            return;

        if (comp.Intensity != 1f)
            emitter.Intensity = comp.Intensity;
    }
}

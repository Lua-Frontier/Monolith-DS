using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Funkystation.Explosion.EntitySystems;

/// <summary>
/// RMC explosion visuals from forky-station #136: flash, shockwave, smoke, embers and flying shrapnel at the epicenter.
/// LuaM: spawned by the explosion system for every explosion with the plain fire, ship shells included,
/// instead of per explosive through ExplosionEffectComponent. The forky prototypes never changed its defaults.
/// </summary>
public abstract partial class SharedExplosionEffectSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = null!;
    [Dependency] private ThrowingSystem _throwing = null!;

    private static readonly EntProtoId[] VisualEffects =
    {
        "ExplosionEffectGrenade",
        "ExplosionEffectGrenadeShockWave",
        // LuaM: ExplosionEffectGrenadeSmoke removed, the dark smoke cloud covered too much
        "ExplosionEffectGrenadeEmbers",
        "ExplosionEffectGrenadeGlowingEmbers",
    };

    private static readonly EntProtoId[] ShrapnelEffects = { "ExplosionEffectShrapnel1", "ExplosionEffectShrapnel2" };

    private const int MinShrapnel = 5;
    private const int MaxShrapnel = 9;
    private const float ShrapnelSpeed = 5f;

    /// <summary>
    /// Spawns the explosion visuals at the given position.
    /// </summary>
    public void SpawnEffects(MapCoordinates epicenter)
    {
        foreach (var effect in VisualEffects)
        {
            Spawn(effect, epicenter);
        }

        foreach (var effect in ShrapnelEffects)
        {
            var shrapnelCount = _random.Next(MinShrapnel, MaxShrapnel);
            for (var i = 0; i < shrapnelCount; i++)
            {
                var direction = _random.NextAngle().ToVec() * 10;
                var shrapnel = Spawn(effect, epicenter);
                _throwing.TryThrow(shrapnel, direction, ShrapnelSpeed / 10);
            }
        }
    }
}

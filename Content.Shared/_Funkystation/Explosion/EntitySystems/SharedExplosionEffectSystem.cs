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

    // LuaM-start
    public const int MediumRadius = 5;
    public const int LargeRadius = 10;

    private static readonly EntProtoId[] MediumVisualEffects =
    {
        "ExplosionEffectLuaMMedium",
        "ExplosionEffectLuaMMediumShockWave",
        "ExplosionEffectLuaMMediumEmbers",
        "ExplosionEffectLuaMMediumGlowingEmbers",
    };

    private static readonly EntProtoId[] LargeVisualEffects =
    {
        "ExplosionEffectLuaMLarge",
        "ExplosionEffectLuaMLargeShockWave",
        "ExplosionEffectLuaMLargeEmbers",
        "ExplosionEffectLuaMLargeGlowingEmbers",
    };
    // LuaM-end

    /// <summary>
    /// Spawns the explosion visuals at the given position.
    /// </summary>
    public void SpawnEffects(MapCoordinates epicenter, int radius = 0) // LuaM: radius picks the effect tier
    {
        // LuaM-start
        var effects = radius >= LargeRadius ? LargeVisualEffects
            : radius >= MediumRadius ? MediumVisualEffects
            : VisualEffects;
        var shrapnelScale = radius >= LargeRadius ? 2.2f : radius >= MediumRadius ? 1.5f : 1f;

        foreach (var effect in effects)
        {
            Spawn(effect, epicenter);
        }

        foreach (var effect in ShrapnelEffects)
        {
            var shrapnelCount = (int) (_random.Next(MinShrapnel, MaxShrapnel) * shrapnelScale);
            for (var i = 0; i < shrapnelCount; i++)
            {
                var direction = _random.NextAngle().ToVec() * 10;
                var shrapnel = Spawn(effect, epicenter);
                _throwing.TryThrow(shrapnel, direction, ShrapnelSpeed * shrapnelScale / 10);
            }
        }
        // LuaM-end
    }
}

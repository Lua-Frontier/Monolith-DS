using System.Diagnostics.CodeAnalysis;
using Content.Shared.Clothing;
using Content.Shared.Maps;
using Content.Shared.Movement.Components;
using Robust.Shared.Audio;
using Robust.Shared.Network;

namespace Content.Shared._LuaM.Clothing;

public sealed partial class SpeedTieredFootstepsSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    // float rounding, so a 0.8 modifier is counted as exactly 20%
    private const float Epsilon = 0.001f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpeedTieredFootstepsComponent, MapInitEvent>(OnMapInit);
    }

    // server only, the tier and the footstep component are networked to the client
    private void OnMapInit(Entity<SpeedTieredFootstepsComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        var tier = ent.Comp.ForcedTier ?? GetTier(ent);
        ent.Comp.Tier = tier;
        Dirty(ent);

        var sound = tier switch
        {
            FootstepsTier.Heavy => ent.Comp.HeavySound,
            FootstepsTier.Medium => ent.Comp.MediumSound,
            _ => ent.Comp.LightSound,
        };

        var footsteps = EnsureComp<FootstepModifierComponent>(ent);
        footsteps.FootstepSoundCollection = sound;
        Dirty(ent, footsteps);
    }

    /// <summary>
    /// The suit's steps for the surface of this tile, if it has its own suit steps.
    /// </summary>
    public bool TryGetSurfaceSound(EntityUid suit, ContentTileDefinition tile, [NotNullWhen(true)] out SoundSpecifier? sound)
    {
        return TryGetSurfaceSound(suit, tile.FootstepSounds, out sound);
    }

    /// <summary>
    /// The suit's steps for a surface with these footsteps, if it has its own suit steps.
    /// </summary>
    public bool TryGetSurfaceSound(EntityUid suit, SoundSpecifier? surface, [NotNullWhen(true)] out SoundSpecifier? sound)
    {
        sound = null;

        if (surface is not SoundCollectionSpecifier { Collection: { } collection } ||
            !TryComp<SpeedTieredFootstepsComponent>(suit, out var comp) ||
            !comp.SurfaceSounds.TryGetValue(collection, out var sounds))
        {
            return false;
        }

        sound = sounds.Get(comp.Tier);
        return true;
    }

    private FootstepsTier GetTier(Entity<SpeedTieredFootstepsComponent> ent)
    {
        var slowdown = 0f;
        if (TryComp<ClothingSpeedModifierComponent>(ent, out var speed))
            slowdown = 1f - MathF.Min(speed.WalkModifier, speed.SprintModifier);

        if (slowdown > ent.Comp.HeavySlowdown + Epsilon)
            return FootstepsTier.Heavy;

        return slowdown > ent.Comp.MediumSlowdown + Epsilon ? FootstepsTier.Medium : FootstepsTier.Light;
    }
}

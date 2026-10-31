using Content.Shared.Clothing;
using Content.Shared.Movement.Components;
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

    // server only, the footstep component is networked to the client
    private void OnMapInit(Entity<SpeedTieredFootstepsComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        var tier = ent.Comp.ForcedTier ?? GetTier(ent);
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

using Content.Shared._LuaM.Mobs;
using Content.Shared.Mobs;
using Robust.Shared.Audio.Systems;

namespace Content.Server._LuaM.Mobs;

public sealed partial class DeathSoundSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeathSoundComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<DeathSoundComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            _audio.PlayPvs(ent.Comp.Sound, ent);
    }
}

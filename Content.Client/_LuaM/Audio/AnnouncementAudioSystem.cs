using Content.Shared._LuaM.Audio;
using Content.Shared._LuaM.CCVar;
using Robust.Client.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;

namespace Content.Client._LuaM.Audio;

public sealed partial class AnnouncementAudioSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public const float AnnouncementMultiplier = 2f;

    private float _volume;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesBefore.Add(typeof(AudioSystem));

        SubscribeLocalEvent<AnnouncementAudioComponent, ComponentStartup>(OnStartup);
        Subs.CVar(_cfg, LuaMCVars.AnnouncementVolume, value => _volume = SharedAudioSystem.GainToVolume(value), true);
    }

    private void OnStartup(Entity<AnnouncementAudioComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<AudioComponent>(ent, out var audio))
            ent.Comp.BaseVolume = audio.Params.Volume;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<AnnouncementAudioComponent, AudioComponent>();
        while (query.MoveNext(out var uid, out var announcement, out var audio))
        {
            _audio.SetVolume(uid, announcement.BaseVolume + _volume, audio);
        }
    }
}

using Content.Shared._LuaM.Audio;
using Robust.Shared.Audio.Components;

namespace Content.Server._LuaM.Audio;

public sealed partial class AnnouncementAudioSystem : EntitySystem
{
    public void Mark((EntityUid Entity, AudioComponent Component)? audio)
    {
        if (audio is { } stream)
            EnsureComp<AnnouncementAudioComponent>(stream.Entity);
    }
}

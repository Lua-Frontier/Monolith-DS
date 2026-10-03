using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Client._LuaM.ItemToggle;

public sealed class ItemToggleActiveSoundSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<ItemToggleActiveSoundComponent, ItemToggleComponent>();
        while (query.MoveNext(out _, out var sound, out var toggle))
        {
            if (toggle.Activated || sound.PlayingStream == null)
                continue;

            sound.PlayingStream = _audio.Stop(sound.PlayingStream);
        }
    }
}

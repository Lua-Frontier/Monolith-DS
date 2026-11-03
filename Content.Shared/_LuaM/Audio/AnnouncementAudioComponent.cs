using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Audio;

[RegisterComponent, NetworkedComponent]
public sealed partial class AnnouncementAudioComponent : Component
{
    [ViewVariables]
    public float BaseVolume;
}

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Showers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShowerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool ToggleShower;

    [DataField]
    public SoundSpecifier EnableShowerSound = new SoundPathSpecifier("/Audio/_Lua/Ambience/Objects/shower_enable.ogg");

    [DataField]
    public SoundSpecifier LoopingSound = new SoundPathSpecifier("/Audio/_Lua/Ambience/Objects/shower_running.ogg");

    public EntityUid? PlayingStream;
}

[Serializable, NetSerializable]
public enum ShowerVisuals : byte
{
    ShowerVisualState,
}

[Serializable, NetSerializable]
public enum ShowerVisualState : byte
{
    Off,
    On
}

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.SectorAnomaly;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SectorDarknessComponent : Component
{
    [DataField, AutoNetworkedField]
    public SoundSpecifier? Music;
}

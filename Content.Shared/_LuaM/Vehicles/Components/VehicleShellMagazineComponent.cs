using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._LuaM.Vehicles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class VehicleShellMagazineComponent : Component
{
    [DataField(required: true)]
    public List<VehicleShellType> Shells = new();

    [DataField, AutoNetworkedField]
    public List<int> Counts = new();

    [DataField, AutoNetworkedField]
    public int Selected;

    [DataField]
    public SoundSpecifier? LoadedSound;

    [DataField]
    public SoundSpecifier? SwitchSound;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? LoadedAt;
}

[DataDefinition]
public sealed partial class VehicleShellType
{
    [DataField(required: true)]
    public string AmmoType = string.Empty;

    [DataField(required: true)]
    public EntProtoId Proto;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public int Capacity = 10;

    [DataField]
    public int StartingCount;
}

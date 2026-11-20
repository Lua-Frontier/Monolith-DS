namespace Content.Server._LuaM.Atmos;

[RegisterComponent]
public sealed partial class AirAlarmModeStampComponent : Component
{
    public uint Tick;

    public string Origin = string.Empty;

    public uint Sequence;
}

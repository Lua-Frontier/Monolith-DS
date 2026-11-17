namespace Content.Server._LuaM.Speech;

[RegisterComponent, Access(typeof(TacticalAccentSystem))]
public sealed partial class TacticalAccentComponent : Component
{
    [DataField]
    public float SuffixChance = 0.15f;
}

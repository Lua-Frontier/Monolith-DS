namespace Content.Server._LuaM.Speech;

[RegisterComponent, Access(typeof(ArcanAccentSystem))]
public sealed partial class ArcanAccentComponent : Component
{
    [DataField]
    public float PrefixChance = 0.1f;
}

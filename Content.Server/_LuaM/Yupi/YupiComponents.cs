namespace Content.Server._LuaM.Yupi;

[RegisterComponent, Access(typeof(YupiSystem))]
public sealed partial class YupiAccountComponent : Component
{
    [ViewVariables]
    public string Code = string.Empty;
}

[RegisterComponent]
public sealed partial class YupiCartridgeComponent : Component
{
}

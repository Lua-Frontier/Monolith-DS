namespace Content.Shared._LuaM.Throwing;

[RegisterComponent]
public sealed partial class ThrowerComponent : Component
{
    [ViewVariables]
    public HashSet<EntityUid> InFlight = new();
}

namespace Content.Server._Mono.AlertLevel;

/// <summary>
/// Way of indicating if a round is before or after war declaration when you join.
/// </summary>
[RegisterComponent]
public sealed partial class WarLevelComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)] public bool PostWar = false;

    /// <summary>
    /// LuaM: one side has declared war and the others have not answered yet (yellow war level).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)] public bool Pending = false;
}

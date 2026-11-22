namespace Content.Shared._LuaM.Traits;

/// <summary>
/// A shut down silicon tries to switch itself back on every so often,
/// the same way as somebody pressing its startup button.
/// </summary>
[RegisterComponent]
public sealed partial class AutoRebootComponent : Component
{
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public TimeSpan NextAttempt;
}

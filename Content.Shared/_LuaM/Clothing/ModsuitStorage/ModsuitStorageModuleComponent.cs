namespace Content.Shared._LuaM.Clothing.ModsuitStorage;

/// <summary>
/// Storage module for a modsuit control unit, replaces its storage grid while installed.
/// The grid must contain <see cref="ModsuitStorageUpgradeComponent.BaseGrid"/>, so stored items keep fitting.
/// </summary>
[RegisterComponent]
public sealed partial class ModsuitStorageModuleComponent : Component
{
    [DataField(required: true)]
    public List<Box2i> Grid = new();
}

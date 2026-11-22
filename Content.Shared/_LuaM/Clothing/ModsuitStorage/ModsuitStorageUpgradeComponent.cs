using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._LuaM.Clothing.ModsuitStorage;

/// <summary>
/// Modsuit control unit whose storage grid is set by the storage module installed under its panel.
/// </summary>
[RegisterComponent, Access(typeof(ModsuitStorageUpgradeSystem))]
public sealed partial class ModsuitStorageUpgradeComponent : Component
{
    public const string SlotId = "storage_module";

    [DataField]
    public ItemSlot ModuleSlot = new();

    /// <summary>
    /// Storage grid used when no module is installed.
    /// </summary>
    [DataField]
    public List<Box2i> BaseGrid = new() { new Box2i(0, 0, 7, 4) };
}

using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Loadouts;

[Prototype]
public sealed partial class LoadoutCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public int Order;

    [DataField]
    public List<string> Slots = new();

    [DataField(required: true)]
    public List<string> Match = new();

    [DataField]
    public List<string> Exclude = new();

    [DataField]
    public List<string> Require = new();
}

using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Delivery;

/// <summary>
/// Glasses that highlight the customers of the orders the wearer has taken.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DeliveryGogglesComponent : Component;

/// <summary>
/// Customers of the orders a courier currently carries. Only the courier gets this state.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DeliveryTargetsComponent : Component
{
    [AutoNetworkedField]
    public List<NetEntity> Targets = new();

    public override bool SendOnlyToOwner => true;
}

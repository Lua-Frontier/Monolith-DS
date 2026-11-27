namespace Content.Server._LuaM.ShipGunAssembly;

/// <summary>
/// Ship weapon assembled from lathe parts on a frame. Sells for the cost of its parts instead of its static price,
/// otherwise printing parts and selling the finished gun would be pure profit.
/// </summary>
[RegisterComponent]
public sealed partial class AssembledShipGunComponent : Component
{
    /// <summary>
    /// Sell price of the assembled weapon, not counting what is loaded into it.
    /// </summary>
    [DataField]
    public double Price;
}

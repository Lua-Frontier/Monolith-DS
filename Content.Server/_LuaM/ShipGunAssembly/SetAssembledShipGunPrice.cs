using Content.Shared.Construction;

namespace Content.Server._LuaM.ShipGunAssembly;

/// <summary>
/// Construction action for the last edge of a ship weapon frame: marks the frame, so the weapon that replaces it
/// keeps the sell price of its parts.
/// </summary>
[DataDefinition]
public sealed partial class SetAssembledShipGunPrice : IGraphAction
{
    [DataField(required: true)]
    public double Price;

    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        entityManager.EnsureComponent<AssembledShipGunComponent>(uid).Price = Price;
    }
}

using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Traits;

/// <summary>
/// Hurts the body when a reagent touches it, like water shorting out an IPC.
/// </summary>
[RegisterComponent]
public sealed partial class ReagentTouchDamageComponent : Component
{
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Water";

    /// <summary>
    /// Damage for every unit of the reagent, resistances are ignored.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier DamagePerUnit = new();
}

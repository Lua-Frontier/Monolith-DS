using Content.Shared.Damage;

namespace Content.Server._LuaM.BioShield;

/// <summary>
/// A shield generator that burns chimera biomass on its grid and keeps new biomass from growing there while the shield is up.
/// </summary>
[RegisterComponent]
public sealed partial class BioShieldComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new() { DamageDict = { ["Heat"] = 10 } };
}

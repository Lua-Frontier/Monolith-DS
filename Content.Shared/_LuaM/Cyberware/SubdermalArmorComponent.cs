using Content.Shared.Damage.Prototypes;

namespace Content.Shared._LuaM.Cyberware;

/// <summary>
/// Armor organ in the subdermal armor slot of the chest.
/// While it is in a body, its modifiers are written into the body's damage protection,
/// and taken back out when it is removed, without touching protection from other sources.
/// Surgeries also use it to find the armor in the chest.
/// </summary>
[RegisterComponent]
public sealed partial class SubdermalArmorComponent : Component
{
    // Not required: surgeries name this component without any fields to find the armor organ.
    [DataField]
    public Dictionary<string, DamageModifierSetPrototype> Modifiers = new();

    /// <summary>
    /// Modifiers the body had under the same keys before, like the dermal armor trait. Restored on removal.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, DamageModifierSetPrototype> Replaced = new();

    /// <summary>
    /// Whether the damage protection component was added by this armor.
    /// </summary>
    [ViewVariables]
    public bool AddedProtection;
}

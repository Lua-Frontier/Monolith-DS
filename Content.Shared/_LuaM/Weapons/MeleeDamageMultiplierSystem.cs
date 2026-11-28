using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Weapons;

/// <summary>
/// Multiplies melee damage through a modifier set instead of the damage itself.
/// Modifier sets are applied after every handler of <see cref="GetMeleeDamageEvent"/> has run,
/// so flat bonuses like claws or calluses are always multiplied too, whatever order the handlers run in.
/// </summary>
public sealed partial class MeleeDamageMultiplierSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly Dictionary<float, DamageModifierSet> _sets = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<DamageTypePrototype>())
            _sets.Clear();
    }

    public void Multiply(ref GetMeleeDamageEvent args, float multiplier)
    {
        if (multiplier == 1f)
            return;

        if (!_sets.TryGetValue(multiplier, out var set))
        {
            set = new DamageModifierSet();
            foreach (var type in _prototypes.EnumeratePrototypes<DamageTypePrototype>())
            {
                set.Coefficients[type.ID] = multiplier;
            }

            _sets[multiplier] = set;
        }

        args.Modifiers.Add(set);
    }
}

using Content.Shared._LuaM.Cyberware;
using Content.Shared.Body.Events;
using Content.Shared.Damage.Components;

namespace Content.Server._LuaM.Cyberware;

/// <summary>
/// Gives the body the protection of its subdermal armor organ and takes it away when the armor is removed.
/// </summary>
public sealed partial class SubdermalArmorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SubdermalArmorComponent, OrganAddedToBodyEvent>(OnAddedToBody);
        SubscribeLocalEvent<SubdermalArmorComponent, OrganRemovedFromBodyEvent>(OnRemovedFromBody);
    }

    private void OnAddedToBody(Entity<SubdermalArmorComponent> ent, ref OrganAddedToBodyEvent args)
    {
        ent.Comp.AddedProtection = !EnsureComp<DamageProtectionBuffComponent>(args.Body, out var protection);
        ent.Comp.Replaced.Clear();

        foreach (var (key, modifier) in ent.Comp.Modifiers)
        {
            if (protection.Modifiers.TryGetValue(key, out var old))
                ent.Comp.Replaced[key] = old;

            protection.Modifiers[key] = modifier;
        }

        Dirty(args.Body, protection);
    }

    private void OnRemovedFromBody(Entity<SubdermalArmorComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (TerminatingOrDeleted(args.OldBody)
            || !TryComp<DamageProtectionBuffComponent>(args.OldBody, out var protection))
        {
            ent.Comp.Replaced.Clear();
            return;
        }

        foreach (var (key, modifier) in ent.Comp.Modifiers)
        {
            // Something else took the key over since, leave it be.
            if (!protection.Modifiers.TryGetValue(key, out var current) || current != modifier)
                continue;

            if (ent.Comp.Replaced.TryGetValue(key, out var old))
                protection.Modifiers[key] = old;
            else
                protection.Modifiers.Remove(key);
        }

        ent.Comp.Replaced.Clear();

        if (ent.Comp.AddedProtection && protection.Modifiers.Count == 0)
            RemComp<DamageProtectionBuffComponent>(args.OldBody);
        else
            Dirty(args.OldBody, protection);
    }
}

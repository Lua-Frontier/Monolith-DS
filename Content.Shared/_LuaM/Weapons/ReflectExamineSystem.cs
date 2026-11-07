using Content.Shared._LuaM.MantisBlades;
using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Weapons.Reflect;

namespace Content.Shared._LuaM.Weapons;

/// <summary>
/// Shows the reflect chance of items when examined.
/// Mantis blades show the chance they get while both are extended.
/// </summary>
public sealed partial class ReflectExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReflectComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<ReflectComponent> ent, ref ExaminedEvent args)
    {
        // Items only: a body with reflection from implants should not give them away.
        if (!HasComp<ItemComponent>(ent))
            return;

        var kind = Loc.GetString(GetKindLoc(ent.Comp.Reflects));

        if (TryComp<MantisBladeComboComponent>(ent, out var combo))
        {
            if (combo.ReflectProb > 0)
                args.PushMarkup(Loc.GetString("reflect-examine-mantis", ("percent", ToPercent(combo.ReflectProb)), ("kind", kind)));
            return;
        }

        if (ent.Comp.ReflectProb <= 0)
            return;

        var key = HasComp<ItemToggleComponent>(ent) ? "reflect-examine-toggle" : "reflect-examine";
        args.PushMarkup(Loc.GetString(key, ("percent", ToPercent(ent.Comp.ReflectProb)), ("kind", kind)));
    }

    private static int ToPercent(float prob)
    {
        return (int) MathF.Round(prob * 100f);
    }

    private static string GetKindLoc(ReflectType reflects)
    {
        var energy = (reflects & ReflectType.Energy) != 0;
        var bullets = (reflects & ReflectType.NonEnergy) != 0;

        if (energy && !bullets)
            return "reflect-examine-kind-energy";

        if (bullets && !energy)
            return "reflect-examine-kind-bullets";

        return "reflect-examine-kind-all";
    }
}

/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Content.Client.Guidebook.Controls;
using Content.Client.Guidebook.Richtext;
using Content.Shared._Mono.ArmorPlate;
using Content.Shared.Armor;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Explosion.Components;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Small comparison tables sourced from resolved prototypes, including inherited components.</summary>
public sealed partial class ArmorStats : BoxContainer, IDocumentTag
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IComponentFactory _factory = default!;

    public ArmorStats()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = this;
        var kind = args.GetValueOrDefault("Kind", "suits");
        var ids = args.GetValueOrDefault("Entities", "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (args.TryGetValue("Title", out var title))
        {
            var button = new Button { Text = title, ToggleMode = true, HorizontalExpand = true };
            var body = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, Visible = false };
            button.OnPressed += _ =>
            {
                if (body.ChildCount == 0)
                    body.AddChild(BuildTable(ids, kind));
                body.Visible = !body.Visible;
            };
            AddChild(button);
            AddChild(body);
        }
        else
            AddChild(BuildTable(ids, kind));
        return true;
    }

    private BoxContainer BuildTable(string[] ids, string kind)
    {
        // BoxContainer measures the fixed sprite first and wraps the remaining text in the
        // actual space left. GridContainer measures both columns at the full page width.
        var table = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 8 };
        foreach (var id in ids)
        {
            if (!_prototypes.TryIndex<EntityPrototype>(id, out var proto))
                continue;
            // Each record has the game's own sprite and localized, unmodified caption.
            var row = new BoxContainer { HorizontalExpand = true, SeparationOverride = 8 };
            var preview = new GuideEntityEmbed(id, true, false) { SetWidth = 128, Margin = new Thickness(4) };
            preview.FindControl<SpriteView>("View").SetSize = new System.Numerics.Vector2(72, 72);
            row.AddChild(preview);
            var text = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, Margin = new Thickness(4) };
            row.AddChild(text);
            table.AddChild(row);
            if (kind == "plates" && proto.TryComp<ArmorPlateItemComponent>(out var plate, _factory))
            {
                Add(text, Loc.GetString("armor-guide-absorption-wear"));
                foreach (var type in plate.AbsorptionRatios.Keys.Union(plate.DamageToPlate.Keys))
                    Add(text, TypeName(type) + ": " + Percent(-plate.AbsorptionRatios.GetValueOrDefault(type)) + " / ×" + Number(plate.DamageToPlate.GetValueOrDefault(type)));
                Add(text, Loc.GetString("armor-guide-durability") + ": " + plate.MaxDurability);
                Add(text, Loc.GetString("armor-guide-speed") + ": " + Change(plate.WalkSpeedModifier) + "/" + Change(plate.SprintSpeedModifier));
                Add(text, Loc.GetString("armor-guide-stamina") + ": " + string.Join(" · ", plate.StaminaDamageMultipliers.Select(p => Loc.GetString("armor-guide-stamina-" + p.Key.ToLowerInvariant()) + " ×" + Number(p.Value))));
            }
            else if (kind == "ammo")
            {
                var projectile = proto;
                if (proto.TryComp<CartridgeAmmoComponent>(out var cartridge, _factory))
                    _prototypes.TryIndex(cartridge.Prototype, out projectile);
                if (projectile != null)
                    Ammo(text, projectile);
            }
            else if (proto.TryComp<ArmorComponent>(out var armor, _factory))
            {
                if (proto.TryComp<ClothingSpeedModifierComponent>(out var speed, _factory))
                    Speed(text, speed.WalkModifier, speed.SprintModifier);
                Add(text, Loc.GetString("armor-examine"));
                foreach (var coefficient in armor.Modifiers.Coefficients.OrderBy(p => DamageOrder(p.Key.Id)))
                {
                    var type = Loc.GetString("armor-damage-type-" + coefficient.Key.Id.ToLowerInvariant());
                    var reduction = MathF.Round((1 - coefficient.Value) * 100, 1);
                    Markup(text, Loc.GetString(reduction < 0 ? "armor-guide-damage-increase" : "armor-coefficient-value",
                        ("type", type), ("value", MathF.Abs(reduction))));
                }
                foreach (var flat in armor.Modifiers.FlatReduction.OrderBy(p => DamageOrder(p.Key.Id)))
                    Markup(text, Loc.GetString("armor-reduction-value",
                        ("type", Loc.GetString("armor-damage-type-" + flat.Key.Id.ToLowerInvariant())), ("value", flat.Value)));
                if (proto.TryComp<StaminaDamageResistanceComponent>(out var stamina, _factory))
                {
                    var reduction = MathF.Round((1 - stamina.Coefficient) * 100, 1);
                    Markup(text, Loc.GetString(reduction < 0 ? "armor-guide-stamina-increase" : "armor-examine-stamina",
                        ("num", MathF.Abs(reduction))));
                }
                if (proto.TryComp<ExplosionResistanceComponent>(out var explosion, _factory) && explosion.Worn)
                {
                    var reduction = MathF.Round((1 - explosion.DamageCoefficient) * 100, 1);
                    Markup(text, Loc.GetString(reduction < 0 ? "armor-guide-explosion-increase" : explosion.Examine,
                        ("value", MathF.Abs(reduction))));
                }
            }
        }
        return table;
    }

    private void Ammo(BoxContainer text, EntityPrototype proto)
    {
        var count = 1;
        if (proto.TryComp<ProjectileSpreadComponent>(out var spread, _factory))
            count = spread.Count;
        DamageSpecifier? damage = null;
        var penetration = 0f;
        var bypass = false;
        if (proto.TryComp<ProjectileComponent>(out var projectile, _factory))
        {
            damage = projectile.Damage;
            penetration = projectile.ArmorPenetration;
            bypass = projectile.IgnoreResistances;
        }
        else if (proto.TryComp<HitscanBasicDamageComponent>(out var hitscan, _factory))
        {
            damage = hitscan.Damage;
            penetration = hitscan.ArmorPenetration;
            bypass = hitscan.IgnoreResistances;
        }
        else if (spread != null && _prototypes.TryIndex(spread.Proto, out var pellet) && pellet.TryComp<ProjectileComponent>(out var pelletDamage, _factory))
        {
            damage = pelletDamage.Damage;
            penetration = pelletDamage.ArmorPenetration;
            bypass = pelletDamage.IgnoreResistances;
        }
        if (damage == null)
        {
            Add(text, Loc.GetString("armor-guide-no-direct"));
            return;
        }
        Add(text, Loc.GetString("armor-guide-direct"));
        foreach (var (type, value) in damage.DamageDict)
            Add(text, TypeName(type) + " " + value);
        Add(text, Loc.GetString("armor-guide-ap") + ": " + (bypass ? Loc.GetString("armor-guide-bypass") : Percent(penetration)));
        if (proto.TryComp<StaminaDamageOnCollideComponent>(out var stamina, _factory))
            Add(text, Loc.GetString("armor-guide-stamina") + ": " + Number(stamina.Damage));
        if (count > 1)
            Add(text, Loc.GetString("armor-guide-pellets", ("count", count)));
    }

    private string TypeName(string id) => _prototypes.TryIndex<DamageTypePrototype>(id, out var type) ? type.LocalizedName : id;
    private static string Number(float number) => number.ToString("0.##", CultureInfo.CurrentCulture);
    private static string Percent(float number) => Number(number * 100) + "%";
    private static string Change(float factor) => (100 * (factor - 1)).ToString("+0.##;-0.##;0", CultureInfo.CurrentCulture) + "%";
    private static int DamageOrder(string id) => id switch
    {
        "Blunt" => 0, "Slash" => 1, "Piercing" => 2,
        "Heat" => 3, "Radiation" => 4, "Caustic" => 5,
        _ => 6,
    };

    private static void Speed(Control parent, float walk, float run)
    {
        var walking = MathF.Round((walk - 1) * 100, 1);
        var running = MathF.Round((run - 1) * 100, 1);
        if (walking == running)
        {
            if (walking != 0)
                Markup(parent, Loc.GetString(walking > 0 ? "clothing-speed-increase-equal-examine" : "clothing-speed-decrease-equal-examine",
                    ("walkSpeed", MathF.Abs(walking)), ("runSpeed", MathF.Abs(running))));
            return;
        }
        if (running != 0)
            Markup(parent, Loc.GetString(running > 0 ? "clothing-speed-increase-run-examine" : "clothing-speed-decrease-run-examine", ("runSpeed", MathF.Abs(running))));
        if (walking != 0)
            Markup(parent, Loc.GetString(walking > 0 ? "clothing-speed-increase-walk-examine" : "clothing-speed-decrease-walk-examine", ("walkSpeed", MathF.Abs(walking))));
    }

    private static void Markup(Control parent, string value)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromMarkupOrThrow(value));
        parent.AddChild(label);
    }

    private static void Add(Control parent, string value)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromUnformatted(value));
        parent.AddChild(label);
    }
}

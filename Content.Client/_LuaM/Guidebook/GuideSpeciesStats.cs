/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Content.Client.Guidebook.Richtext;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Unmodified playable species, measured against the human prototype.</summary>
public sealed partial class GuideSpeciesStats : BoxContainer, IDocumentTag
{
    private static readonly EntProtoId MobHumanProto = "MobHuman";

    [Dependency] private IPrototypeManager _prototypes = default!;
    public GuideSpeciesStats()
    { IoCManager.InjectDependencies(this); Orientation = LayoutOrientation.Vertical; HorizontalExpand = true; SeparationOverride = 6; }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = this;
        SpeciesPrototype? selected = null;
        if (args.TryGetValue("Species", out var speciesId)
            && !_prototypes.TryIndex(speciesId, out selected))
            return false;
        if (!_prototypes.TryIndex(MobHumanProto, out var human)) return true;
        var speciesToShow = selected != null
            ? new[] { selected }
            : _prototypes.EnumeratePrototypes<SpeciesPrototype>().Where(s => s.RoundStart).OrderBy(s => Loc.GetString(s.Name)).ToArray();
        foreach (var species in speciesToShow)
        {
            if (!_prototypes.TryIndex(species.Prototype, out var mob)) continue;
            var details = new List<string>();
            if (Part<MovementSpeedModifierComponent>(mob, "MovementSpeedModifier") is { } speed
                && Part<MovementSpeedModifierComponent>(human, "MovementSpeedModifier") is { } humanSpeed)
            {
                var walk = speed.BaseWalkSpeed;
                var sprint = speed.BaseSprintSpeed;
                details.Add("Ходьба: " + walk.ToString("0.##", CultureInfo.CurrentCulture) + " м/с (" + Relative(walk, humanSpeed.BaseWalkSpeed) + ")");
                details.Add("Бег: " + sprint.ToString("0.##", CultureInfo.CurrentCulture) + " м/с (" + Relative(sprint, humanSpeed.BaseSprintSpeed) + ")");
            }
            if (Part<StaminaComponent>(mob, "Stamina") is { } stamina)
                details.Add("Порог оглушения от выносливости: " + stamina.CritThreshold);
            if (Part<MobThresholdsComponent>(mob, "MobThresholds") is { } thresholds
                && Part<MobThresholdsComponent>(human, "MobThresholds") is { } humanThresholds)
            foreach (var (amount, state) in thresholds.Thresholds)
            {
                if (amount <= 0) continue;
                var baseline = humanThresholds.Thresholds.FirstOrDefault(p => p.Value == state).Key.Float();
                var name = state.ToString() == "Critical" ? "Критический урон" : state.ToString() == "Dead" ? "Смерть" : "Порог состояния";
                details.Add(name + ": " + amount + (baseline > 0 ? " (" + Relative(amount.Float(), baseline) + ")" : ""));
            }
            var modifiers = Modifiers(mob);
            var baseModifiers = Modifiers(human);
            foreach (var type in (modifiers?.Coefficients.Keys.AsEnumerable() ?? Enumerable.Empty<ProtoId<DamageTypePrototype>>())
                         .Union(baseModifiers?.Coefficients.Keys.AsEnumerable() ?? Enumerable.Empty<ProtoId<DamageTypePrototype>>()).OrderBy(t => t.Id))
            {
                var coefficient = modifiers?.Coefficients.GetValueOrDefault(type, 1) ?? 1;
                var baseline = baseModifiers?.Coefficients.GetValueOrDefault(type, 1) ?? 1;
                if (Math.Abs(coefficient - baseline) < 0.001) continue;
                var damage = _prototypes.Index(type);
                details.Add("Получаемый урон, " + damage.LocalizedName + ": " + Relative(coefficient, baseline));
            }
            if (!details.Any(d => d.StartsWith("Получаемый урон,"))) details.Add("Получаемый урон: без отличий от человека.");
            var header = new GuideCatalogHeader(Loc.GetString(species.Name), selected == null ? string.Join(" · ", details.Take(2)) : null) { Expanded = selected != null };
            var body = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, Visible = selected != null };
            foreach (var line in details)
            { var label = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(6) }; label.SetMessage(line); body.AddChild(label); }
            header.OnPressed += _ => { body.Visible = !body.Visible; header.Expanded = body.Visible; };
            AddChild(header); AddChild(body);
        }
        return true;
    }

    private static T? Part<T>(EntityPrototype proto, string name) where T : class
        => proto.Components.TryGetValue(name, out var entry) ? entry.Component as T : null;

    private DamageModifierSetPrototype? Modifiers(EntityPrototype proto)
        => Part<DamageableComponent>(proto, "Damageable")?.DamageModifierSetId is { } id && _prototypes.TryIndex(id, out var set) ? set : null;

    private static string Relative(float value, float human)
        => human == 0 ? "—" : (100 * (value / human - 1)).ToString("+0.#;-0.#;0", CultureInfo.CurrentCulture) + "%";
}

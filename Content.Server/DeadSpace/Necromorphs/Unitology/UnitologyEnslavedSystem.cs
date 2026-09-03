// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.Components;
using Content.Shared.Cloning;
using Content.Shared.DeadSpace.Necromorphs.Unitology.Components;
using Content.Shared.Humanoid;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared._Shitmed.Body.Components;
using Content.Server.Humanoid;
using Content.Shared.Tag;
using System.Linq;

namespace Content.Server.DeadSpace.Necromorphs.Unitology;

public sealed class UnitologyEnslavedSystem : EntitySystem
{
    private static readonly Color NecromorphSkinColor = new(0.55f, 0.18f, 0.18f);

    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private readonly SharedSubdermalImplantSystem _implants = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly UnitologyRoleSystem _roles = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UnitologyEnslavedComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<UnitologyEnslavedComponent, CloningEvent>(OnCloning);
        SubscribeLocalEvent<MindShieldComponent, ComponentInit>(OnMindShieldAdded);
    }

    private void OnComponentInit(EntityUid uid, UnitologyEnslavedComponent comp, ComponentInit args)
    {
        if (!TryAssignLivingHead(uid, comp))
        {
            RemCompDeferred<UnitologyEnslavedComponent>(uid);
            return;
        }

        RemoveMindShield(uid);
        _roles.GrantEnslaved(uid);

        if (comp.IsTransformed)
        {
            ApplySpaceProtection(uid, comp);
            return;
        }

        if (!TryComp<HumanoidAppearanceComponent>(uid, out var appearance))
            return;

        comp.OriginalEyeColor = appearance.EyeColor;
        comp.OriginalSkinColor = appearance.SkinColor;
        comp.OriginalCustomBaseLayers = new(appearance.CustomBaseLayers);

        appearance.EyeColor = Color.Red;
        Dirty(uid, appearance);
        _humanoidAppearance.SetSkinColor(uid, NecromorphSkinColor, verify: false, humanoid: appearance);

        ApplySpaceProtection(uid, comp);
        comp.IsTransformed = true;
        Dirty(uid, comp);
    }

    private void OnCloning(Entity<UnitologyEnslavedComponent> ent, ref CloningEvent args)
    {
        var cloneComp = new UnitologyEnslavedComponent
        {
            StatusIcon = ent.Comp.StatusIcon,
            OriginalCustomBaseLayers = new(ent.Comp.OriginalCustomBaseLayers),
            OriginalSkinColor = ent.Comp.OriginalSkinColor,
            OriginalEyeColor = ent.Comp.OriginalEyeColor,
            IsTransformed = ent.Comp.IsTransformed,
            HeadUnitolog = ent.Comp.HeadUnitolog,
        };

        AddComp(args.Target, cloneComp);
    }

    private void OnMindShieldAdded(Entity<MindShieldComponent> ent, ref ComponentInit args)
    {
        if (!TryComp<UnitologyEnslavedComponent>(ent, out var enslaved))
            return;

        if (TryAssignLivingHead(ent, enslaved))
        {
            RemoveMindShield(ent);
            return;
        }

        FreeSlave(ent, enslaved);
    }

    private bool TryAssignLivingHead(EntityUid uid, UnitologyEnslavedComponent comp)
    {
        if (comp.HeadUnitolog is { } head && HasComp<UnitologyHeadComponent>(head) && _mobState.IsAlive(head))
            return true;

        var heads = EntityQueryEnumerator<UnitologyHeadComponent>();
        while (heads.MoveNext(out head, out _))
        {
            if (!_mobState.IsAlive(head))
                continue;

            comp.HeadUnitolog = head;
            Dirty(uid, comp);
            return true;
        }

        return false;
    }

    private void ApplySpaceProtection(EntityUid uid, UnitologyEnslavedComponent comp)
    {
        if (!HasComp<BreathingImmunityComponent>(uid))
        {
            AddComp<BreathingImmunityComponent>(uid);
            comp.AddedBreathingImmunity = true;
        }

        if (!HasComp<PressureImmunityComponent>(uid))
        {
            AddComp<PressureImmunityComponent>(uid);
            comp.AddedPressureImmunity = true;
        }
    }

    public void FreeSlave(EntityUid uid, UnitologyEnslavedComponent comp)
    {
        RestoreTransformation(uid, comp);
        _roles.RemoveUnitology(uid);
        RemCompDeferred<UnitologyEnslavedComponent>(uid);
    }

    private void RestoreTransformation(EntityUid uid, UnitologyEnslavedComponent comp)
    {
        if (comp.IsTransformed && TryComp<HumanoidAppearanceComponent>(uid, out var appearance))
        {
            appearance.EyeColor = comp.OriginalEyeColor;
            Dirty(uid, appearance);
            _humanoidAppearance.SetSkinColor(uid, comp.OriginalSkinColor, verify: false, humanoid: appearance);

            foreach (var (layer, info) in comp.OriginalCustomBaseLayers)
            {
                _humanoidAppearance.SetBaseLayerColor(uid, layer, info.Color);
                _humanoidAppearance.SetBaseLayerId(uid, layer, info.Id);
            }
        }

        if (comp.AddedBreathingImmunity)
            RemComp<BreathingImmunityComponent>(uid);

        if (comp.AddedPressureImmunity)
            RemComp<PressureImmunityComponent>(uid);

        comp.IsTransformed = false;
        comp.HeadUnitolog = null;
        Dirty(uid, comp);
    }

    private void RemoveMindShield(EntityUid uid)
    {
        if (TryComp<ImplantedComponent>(uid, out var implanted))
        {
            foreach (var implant in implanted.ImplantContainer.ContainedEntities.ToList())
            {
                if (!_tags.HasTag(implant, "MindShield"))
                    continue;

                _implants.ForceRemove(uid, implant);
            }
        }

        RemCompDeferred<MindShieldComponent>(uid);
    }
}

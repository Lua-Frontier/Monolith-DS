using Content.Shared.Eye.Blinding.Systems;

namespace Content.Shared._LuaM.Traits;

/// <summary>
/// Watery eyes from the fur allergy. Shared so the blur stays right when glasses are put on or off.
/// </summary>
public sealed partial class FurAllergyBlurSystem : EntitySystem
{
    [Dependency] private BlurryVisionSystem _blurry = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FurAllergyComponent, GetBlurEvent>(OnGetBlur);
    }

    private void OnGetBlur(Entity<FurAllergyComponent> ent, ref GetBlurEvent args)
    {
        if (ent.Comp.Stage != FurAllergyStage.None)
            args.Blur += ent.Comp.Blur;
    }

    public void SetStage(Entity<FurAllergyComponent> ent, FurAllergyStage stage)
    {
        if (ent.Comp.Stage == stage)
            return;

        ent.Comp.Stage = stage;
        Dirty(ent);
        _blurry.UpdateBlurMagnitude(ent.Owner);
    }
}

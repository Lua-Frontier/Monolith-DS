using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;

namespace Content.Shared._Lua.HiddenBlades;

public abstract partial class SharedHiddenBladesSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HiddenBladesComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnToggled(Entity<HiddenBladesComponent> ent, ref ItemToggledEvent args)
    {
        UpdateNameAndDescription(ent, args.Activated);

        if (!TryComp<ClothingComponent>(ent, out var clothing))
            return;

        _clothing.SetEquippedPrefix(ent, args.Activated ? "activated" : null, clothing);

        if (args.User is { } user)
            _popup.PopupClient(Loc.GetString(args.Activated ? ent.Comp.ActivatedPopUp : ent.Comp.DeactivatedPopUp), ent, user);
    }

    private void UpdateNameAndDescription(Entity<HiddenBladesComponent> ent, bool activated)
    {
        if (MetaData(ent).EntityPrototype is not { } prototype)
            return;

        _metaData.SetEntityName(ent, activated ? Loc.GetString(ent.Comp.ActivatedName) : prototype.Name);
        _metaData.SetEntityDescription(ent, activated ? Loc.GetString(ent.Comp.ActivatedDescription) : prototype.Description);
    }
}

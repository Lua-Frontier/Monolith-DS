using Content.Shared.Power;
using Robust.Client.GameObjects;

namespace Content.Client._Lua.EmpGenerator;

public sealed partial class EmpGeneratorVisualsSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmpGeneratorVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<EmpGeneratorVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        var spriteEnt = (ent.Owner, sprite);

        if (_appearance.TryGetData<PowerChargeStatus>(ent, PowerChargeVisuals.State, out var status, args.Component)
            && ent.Comp.SpriteMap.TryGetValue(status, out var baseState)
            && _sprite.LayerMapTryGet(spriteEnt, EmpGeneratorVisualLayers.Base, out var baseLayer, false))
        {
            _sprite.LayerSetRsiState(spriteEnt, baseLayer, baseState);
        }

        if (!_appearance.TryGetData<float>(ent, PowerChargeVisuals.Charge, out var charge, args.Component)
            || !_sprite.LayerMapTryGet(spriteEnt, EmpGeneratorVisualLayers.Core, out var coreLayer, false))
            return;

        foreach (var threshold in ent.Comp.Thresholds)
        {
            if (charge >= threshold.MaxCharge)
                continue;

            _sprite.LayerSetVisible(spriteEnt, coreLayer, threshold.Visible);
            if (threshold.State != null)
                _sprite.LayerSetRsiState(spriteEnt, coreLayer, threshold.State);
            break;
        }
    }
}

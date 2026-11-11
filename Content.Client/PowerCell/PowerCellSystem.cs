using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics; // LuaM

namespace Content.Client.PowerCell;

[UsedImplicitly]
public sealed partial class PowerCellSystem : SharedPowerCellSystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PowerCellVisualsComponent, AppearanceChangeEvent>(OnPowerCellVisualsChange);
    }

    /// <inheritdoc/>
    public override bool HasActivatableCharge(EntityUid uid, PowerCellDrawComponent? battery = null, PowerCellSlotComponent? cell = null,
        EntityUid? user = null)
    {
        if (!Resolve(uid, ref battery, ref cell, false))
            return true;

        return battery.CanUse;
    }

    /// <inheritdoc/>
    public override bool HasDrawCharge(
        EntityUid uid,
        PowerCellDrawComponent? battery = null,
        PowerCellSlotComponent? cell = null,
        EntityUid? user = null)
    {
        if (!Resolve(uid, ref battery, ref cell, false))
            return true;

        return battery.CanDraw;
    }

    private void OnPowerCellVisualsChange(EntityUid uid, PowerCellVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_sprite.LayerExists((uid, args.Sprite), PowerCellVisualLayers.Unshaded))
            return;

        if (_appearance.TryGetData<byte>(uid, PowerCellVisuals.ChargeLevel, out var level, args.Component))
        {
            if (level == 0 || !component.ChargeBars && level == 1) // LuaM
            {
                _sprite.LayerSetVisible((uid, args.Sprite), PowerCellVisualLayers.Unshaded, false);
                return;
            }

            _sprite.LayerSetVisible((uid, args.Sprite), PowerCellVisualLayers.Unshaded, true); // LuaM: false > true
            // LuaM-start
            var state = component.ChargeBars ? $"o{level}" : $"o{level - 2}";
            if (_sprite.LayerMapTryGet((uid, args.Sprite), PowerCellVisualLayers.Unshaded, out var index, false)
                && _sprite.LayerGetEffectiveRsi((uid, args.Sprite), index) is { } rsi
                && !rsi.TryGetState(state, out _))
            {
                // --  у многих спрайтов есть только o1/o2: без подбора ближайшего состояния слой рисует красный Error
                var fallback = FindChargeState(rsi, level);
                if (fallback == null)
                {
                    _sprite.LayerSetVisible((uid, args.Sprite), PowerCellVisualLayers.Unshaded, false);
                    return;
                }

                state = fallback;
            }

            _sprite.LayerSetRsiState((uid, args.Sprite), PowerCellVisualLayers.Unshaded, state);
            // LuaM-end
        }
    }

    // LuaM-start
    private static string? FindChargeState(RSI rsi, byte level)
    {
        const int maxState = 4;
        var count = 0;
        for (var i = 0; i <= maxState; i++)
        {
            if (rsi.TryGetState($"o{i}", out _))
                count++;
        }

        if (count == 0)
            return null;

        var target = Math.Clamp((int) MathF.Ceiling(level / (float) maxState * count), 1, count);
        for (var i = 0; i <= maxState; i++)
        {
            if (rsi.TryGetState($"o{i}", out _) && --target == 0)
                return $"o{i}";
        }

        return null;
    }
    // LuaM-end

    private enum PowerCellVisualLayers : byte
    {
        Base,
        Unshaded,
    }
}

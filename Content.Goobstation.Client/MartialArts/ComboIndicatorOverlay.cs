// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Goobstation.Client.MartialArts;

/// <summary>
/// Shows the last attacks of the combo chain next to the cursor.
/// </summary>
public sealed class ComboIndicatorOverlay : Overlay
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    private static readonly ResPath IntentsRsi = new("/Textures/_Goobstation/Interface/Misc/intents.rsi");

    private static readonly Color IconColor = Color.White.WithAlpha(0.75f);

    private readonly Dictionary<ComboAttackType, Texture> _icons = new();

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public ComboIndicatorOverlay()
    {
        IoCManager.InjectDependencies(this);

        var rsi = _cache.GetResource<RSIResource>(IntentsRsi).RSI;
        foreach (var type in Enum.GetValues<ComboAttackType>())
        {
            if (rsi.TryGetState(type.ToString().ToLowerInvariant(), out var state))
                _icons[type] = state.Frame0;
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player
            || !_entMan.TryGetComponent(player, out CanPerformComboComponent? combo)
            || combo.LastAttacks.Count == 0)
            return;

        var mousePos = _input.MouseScreenPosition;
        if (mousePos.Window == WindowId.Invalid)
            return;

        var offset = _cfg.GetCVar(CCVars.HudHeldItemOffset);
        var count = combo.LastAttacks.Count;

        for (var i = 0; i < count; i++)
        {
            if (!_icons.TryGetValue(combo.LastAttacks[i], out var icon))
                continue;

            var size = icon.Size;
            var iconOffset = new Vector2(-offset, (2f * i + 1f - count) * size.Y / 1.8f);
            args.ScreenHandle.DrawTextureRect(icon,
                UIBox2.FromDimensions(mousePos.Position - size / 2 + iconOffset, size),
                IconColor);
        }
    }
}

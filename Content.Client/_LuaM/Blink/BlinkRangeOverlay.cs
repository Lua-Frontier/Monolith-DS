// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._LuaM.Blink;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client._LuaM.Blink;

public sealed class BlinkRangeOverlay(
    IEntityManager entities,
    IPlayerManager player,
    BlinkRangeOverlay.TryGetBlinkItem getItem) : Overlay
{
    public delegate bool TryGetBlinkItem(EntityUid user, out EntityUid item, out BlinkItemComponent component);

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (player.LocalEntity is not { } user ||
            !getItem(user, out _, out var blink) ||
            !entities.TryGetComponent<TransformComponent>(user, out var xform) ||
            xform.MapID != args.MapId)
            return;

        var transform = entities.System<SharedTransformSystem>();
        var center = transform.GetWorldPosition(xform);
        const int segments = 56;
        var color = Color.FromHex("#66d9ff99");

        for (var i = 0; i < segments; i += 2)
        {
            var first = MathF.Tau * i / segments;
            var second = MathF.Tau * (i + 1) / segments;
            var from = center + new Vector2(MathF.Cos(first), MathF.Sin(first)) * blink.Range;
            var to = center + new Vector2(MathF.Cos(second), MathF.Sin(second)) * blink.Range;
            args.WorldHandle.DrawLine(from, to, color);
        }
    }
}

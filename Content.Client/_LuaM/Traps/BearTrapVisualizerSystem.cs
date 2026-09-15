// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._LuaM.Traps;
using Robust.Client.GameObjects;

namespace Content.Client._LuaM.Traps;

public sealed class BearTrapVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprites = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BearTrapComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var trap, out var sprite))
        {
            if (!MathHelper.CloseTo(sprite.Color.A, trap.Opacity))
                _sprites.SetColor((uid, sprite), sprite.Color.WithAlpha(trap.Opacity));
        }
    }
}

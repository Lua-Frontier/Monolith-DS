// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._LuaM.Sticky;
using Content.Shared.Sticky.Components;
using Robust.Client.GameObjects;

namespace Content.Client._LuaM.Sticky;

public sealed class ScaleWhenStuckSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScaleWhenStuckComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ScaleWhenStuckComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    private void OnInit(Entity<ScaleWhenStuckComponent> ent, ref ComponentInit args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            ent.Comp.OriginalScale = sprite.Scale;
    }

    private void OnAppearanceChanged(Entity<ScaleWhenStuckComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || ent.Comp.OriginalScale is not { } originalScale)
            return;

        var scale = originalScale;
        if (TryComp<StickyComponent>(ent, out var sticky) &&
            sticky.StuckTo is { } target &&
            HasComp<StickySurfaceOverrideComponent>(target))
        {
            scale *= ent.Comp.Scale;
        }

        _sprite.SetScale((ent.Owner, args.Sprite), scale);
    }
}

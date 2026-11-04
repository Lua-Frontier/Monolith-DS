using System.Linq;
using Content.Client.Items;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._LuaM.SprayPainter;
using Content.Shared.Decals;
using Content.Shared.SprayPainter;
using Content.Shared.SprayPainter.Components;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.SprayPainter;

/// <summary>
/// Decal list and item status for decal painting, ported from the upstream spray painter rework.
/// </summary>
public sealed partial class SprayPainterSystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    public List<SprayPainterDecalEntry> Decals { get; } = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.ItemStatus<SprayPainterComponent>(ent => new DecalModeStatusControl(ent));
        SubscribeLocalEvent<SprayPainterComponent, AfterAutoHandleStateEvent>(OnStateUpdate);

        CacheDecals();
    }

    private void OnStateUpdate(Entity<SprayPainterComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_ui.TryGetOpenUi(ent.Owner, SprayPainterUiKey.Key, out var bui))
            bui.Update();
    }

    protected override void OnDecalPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<DecalPrototype>())
            CacheDecals();
    }

    private void CacheDecals()
    {
        Decals.Clear();
        foreach (var proto in Proto.EnumeratePrototypes<DecalPrototype>().OrderBy(x => x.ID))
        {
            if (!SprayPainterDecals.IsPaintable(proto))
                continue;

            Decals.Add(new SprayPainterDecalEntry(proto.ID, proto.Sprite));
        }
    }

    private sealed class DecalModeStatusControl : Control
    {
        private readonly RichTextLabel _label;
        private readonly Entity<SprayPainterComponent> _entity;
        private DecalPaintMode? _lastMode;

        public DecalModeStatusControl(Entity<SprayPainterComponent> ent)
        {
            _entity = ent;
            _label = new RichTextLabel { StyleClasses = { StyleNano.StyleClassItemStatus } };
            AddChild(_label);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            if (_entity.Comp.DecalMode == _lastMode)
                return;

            _lastMode = _entity.Comp.DecalMode;

            var mode = _entity.Comp.DecalMode switch
            {
                DecalPaintMode.Add => "spray-painter-item-status-add",
                DecalPaintMode.Remove => "spray-painter-item-status-remove",
                _ => "spray-painter-item-status-off",
            };

            _label.SetMarkupPermissive(Robust.Shared.Localization.Loc.GetString("spray-painter-item-status-label",
                ("mode", Robust.Shared.Localization.Loc.GetString(mode))));
        }
    }
}

/// <summary>
/// A spray paintable decal, mapped by ID.
/// </summary>
public sealed record SprayPainterDecalEntry(string Name, SpriteSpecifier Sprite);

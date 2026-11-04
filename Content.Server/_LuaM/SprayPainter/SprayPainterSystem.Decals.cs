using Content.Server.Administration.Logs;
using Content.Server.Decals;
using Content.Server.Popups;
using Content.Shared._LuaM.SprayPainter;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Database;
using Content.Shared.Decals;
using Content.Shared.Interaction;
using Content.Shared.SprayPainter.Components;
using Robust.Server.GameObjects;

namespace Content.Server.SprayPainter;

/// <summary>
/// Paints and removes floor decals, ported from the upstream spray painter rework.
/// </summary>
public sealed partial class SprayPainterSystem
{
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    private void OnFloorAfterInteract(Entity<SprayPainterComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target != null)
            return;

        if (ent.Comp.DecalMode is not (DecalPaintMode.Add or DecalPaintMode.Remove))
            return;

        args.Handled = true;

        if (!HasPaint(ent, ent.Comp.DecalChargeCost, args.User, predicted: false))
            return;

        var position = args.ClickLocation;
        if (ent.Comp.SnapDecals)
            position = position.SnapToGrid(EntityManager);

        if (ent.Comp.DecalMode == DecalPaintMode.Add)
        {
            // decals are anchored by their bottom left corner
            position = position.Offset(new(-0.5f));

            if (!_decals.TryAddDecal(ent.Comp.SelectedDecal, position, out _, ent.Comp.SelectedDecalColor, Angle.FromDegrees(ent.Comp.SelectedDecalAngle), 0, false))
                return;

            _adminLog.Add(LogType.CrayonDraw, LogImpact.Low, $"{ToPrettyString(args.User):user} painted a {ent.Comp.SelectedDecal} at {position}");
        }
        else
        {
            if (_transform.GetGrid(args.ClickLocation) is not { } grid || !TryComp<DecalGridComponent>(grid, out var decalGrid))
            {
                _popup.PopupEntity(Loc.GetString("spray-painter-interact-nothing-to-remove"), args.User, args.User);
                return;
            }

            var decals = _decals.GetDecalsInRange(grid, position.Position, validDelegate: IsDecalRemovable);
            if (decals.Count == 0)
            {
                _popup.PopupEntity(Loc.GetString("spray-painter-interact-nothing-to-remove"), args.User, args.User);
                return;
            }

            foreach (var decal in decals)
            {
                _decals.RemoveDecal(grid, decal.Index, decalGrid);
            }

            _adminLog.Add(LogType.CrayonDraw, LogImpact.Low, $"{ToPrettyString(args.User):user} removed {decals.Count} decals at {position}");
        }

        TryUsePaint(ent, ent.Comp.DecalChargeCost);
        Audio.PlayPvs(ent.Comp.SpraySound, ent);
    }

    /// <summary>
    /// Only the decals the painter can draw can be scrubbed off with it.
    /// </summary>
    private bool IsDecalRemovable(Decal decal)
    {
        return Proto.TryIndex<DecalPrototype>(decal.Id, out var proto) && SprayPainterDecals.IsPaintable(proto);
    }
}

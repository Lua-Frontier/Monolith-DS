using Content.Shared._LuaM.SprayPainter;
using Content.Shared.Charges.Systems;
using Content.Shared.Database;
using Content.Shared.SprayPainter.Components;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.SprayPainter;

/// <summary>
/// Decal painting, ported from the upstream spray painter rework.
/// </summary>
public abstract partial class SharedSprayPainterSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] protected SharedChargesSystem Charges = default!;

    /// <summary>
    /// Checks the painter has enough paint for a job, telling the user when it does not.
    /// </summary>
    protected bool HasPaint(EntityUid painter, int cost, EntityUid user, bool predicted = true)
    {
        if (!Charges.HasInsufficientCharges(painter, cost))
            return true;

        var msg = Loc.GetString("spray-painter-interact-no-charges");
        if (predicted)
            _popup.PopupClient(msg, user, user);
        else
            _popup.PopupEntity(msg, user, user);
        return false;
    }

    /// <summary>
    /// Spends paint for a finished job. Painters without charges paint for free.
    /// </summary>
    protected bool TryUsePaint(EntityUid painter, int cost)
    {
        if (Charges.HasInsufficientCharges(painter, cost))
            return false;

        Charges.UseCharges(painter, cost);
        return true;
    }

    private void OnPainterGetAltVerbs(Entity<SprayPainterComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.Using.HasValue)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("spray-painter-verb-toggle-decals"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () => TogglePaintDecals(ent, user),
            Impact = LogImpact.Low,
        });
    }

    /// <summary>
    /// Cycles the floor mode: off, add, remove.
    /// </summary>
    private void TogglePaintDecals(Entity<SprayPainterComponent> ent, EntityUid user)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        float pitch;
        switch (ent.Comp.DecalMode)
        {
            case DecalPaintMode.Add:
                ent.Comp.DecalMode = DecalPaintMode.Remove;
                pitch = 1.2f;
                break;
            case DecalPaintMode.Remove:
                ent.Comp.DecalMode = DecalPaintMode.Off;
                pitch = 0.8f;
                break;
            default:
                ent.Comp.DecalMode = DecalPaintMode.Add;
                pitch = 1.0f;
                break;
        }
        Dirty(ent);

        Audio.PlayPredicted(ent.Comp.SoundSwitchDecalMode, ent, user, ent.Comp.SoundSwitchDecalMode.Params.WithPitchScale(pitch));
    }

    protected virtual void OnDecalPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
    }

    private void OnSetDecal(Entity<SprayPainterComponent> ent, ref SprayPainterSetDecalMessage args)
    {
        if (!Proto.HasIndex(args.DecalPrototype))
            return;

        ent.Comp.SelectedDecal = args.DecalPrototype;
        Dirty(ent);
    }

    private void OnSetDecalColor(Entity<SprayPainterComponent> ent, ref SprayPainterSetDecalColorMessage args)
    {
        ent.Comp.SelectedDecalColor = args.Color;
        Dirty(ent);
    }

    private void OnSetDecalAngle(Entity<SprayPainterComponent> ent, ref SprayPainterSetDecalAngleMessage args)
    {
        ent.Comp.SelectedDecalAngle = args.Angle;
        Dirty(ent);
    }

    private void OnSetDecalSnap(Entity<SprayPainterComponent> ent, ref SprayPainterSetDecalSnapMessage args)
    {
        ent.Comp.SnapDecals = args.Snap;
        Dirty(ent);
    }
}

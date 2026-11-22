using Content.Server.Shuttles.Events;
using Content.Shared.Shuttles.Systems;
using Content.Server._LuaM.Shuttles; // LuaM
using Content.Shared.Shuttles.Components; // LuaM

namespace Content.Server._Mono.Detection;

public sealed partial class ApplyIFFFlagsToDockedShipsSystem : EntitySystem
{
    [Dependency] private SharedShuttleSystem _shuttle = default!;
    public override void Initialize()
    {
        base.Initialize();

        // We will run the event if dock A has the component.
        SubscribeLocalEvent<DockEvent>(OnDock);
        SubscribeLocalEvent<UndockEvent>(OnUndock);
    }

    private void OnDock(DockEvent args)
    {
        if (TryComp<ApplyIFFFlagsToDockedShipsComponent>(args.GridAUid, out var iffCompA))
            ApplyFlags(args.GridBUid, iffCompA);
        if (TryComp<ApplyIFFFlagsToDockedShipsComponent>(args.GridBUid, out var iffCompB))
            ApplyFlags(args.GridAUid, iffCompB);
    }

    private void OnUndock(UndockEvent args)
    {
        if (TryComp<ApplyIFFFlagsToDockedShipsComponent>(args.GridAUid, out var iffCompA))
            ApplyFlags(args.GridBUid, iffCompA, false);
        if (TryComp<ApplyIFFFlagsToDockedShipsComponent>(args.GridBUid, out var iffCompB))
            ApplyFlags(args.GridAUid, iffCompB, false);
    }

    public void ApplyFlags(EntityUid gridUid, ApplyIFFFlagsToDockedShipsComponent iffComp, bool applying = true)
    {
        // LuaM-start: undocking takes back only the flags docking added, a ship hidden on its own stays hidden
        if (applying)
        {
            var current = TryComp<IFFComponent>(gridUid, out var iff) ? iff.Flags : IFFFlags.None;
            var added = iffComp.Flags & ~current;
            if (added == IFFFlags.None)
                return;

            EnsureComp<DockingIFFFlagsComponent>(gridUid).Flags |= added;
            _shuttle.AddIFFFlag(gridUid, added);
            return;
        }

        if (!TryComp<DockingIFFFlagsComponent>(gridUid, out var docking))
            return;

        _shuttle.RemoveIFFFlag(gridUid, docking.Flags & iffComp.Flags);
        docking.Flags &= ~iffComp.Flags;
        if (docking.Flags == IFFFlags.None)
            RemComp<DockingIFFFlagsComponent>(gridUid);
        // LuaM-end
    }
}

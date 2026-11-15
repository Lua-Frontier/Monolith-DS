using Content.Shared._LuaM.Throwing;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Shared.Containers;

// LuaM: moved to Shared and predicted
public sealed partial class ThrowInsertContainerSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ISharedPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThrowInsertContainerComponent, ThrowHitByEvent>(OnThrowCollide);
    }

    private void OnThrowCollide(Entity<ThrowInsertContainerComponent> ent, ref ThrowHitByEvent args)
    {
        var container = _containerSystem.GetContainer(ent, ent.Comp.ContainerId);

        if (!_containerSystem.CanInsert(args.Thrown, container))
            return;

        var beforeThrowArgs = new BeforeThrowInsertEvent(args.Thrown);
        RaiseLocalEvent(ent, ref beforeThrowArgs);

        if (beforeThrowArgs.Cancelled)
            return;

        var thrower = HasComp<PredictedThrownItemComponent>(args.Thrown) ? args.Component.Thrower : null;
        var feedback = _net.IsServer || thrower != null && thrower == _player.LocalEntity;

        var seed = SharedRandomExtensions.HashCodeCombine(
            GetNetEntity(args.Thrown).Id,
            GetNetEntity(ent).Id,
            (args.Component.ThrownTime ?? TimeSpan.Zero).GetHashCode());

        if (new System.Random(seed).Prob(ent.Comp.Probability))
        {
            if (feedback)
            {
                _audio.PlayPredicted(ent.Comp.MissSound, ent, thrower);
                _popup.PopupPredicted(Loc.GetString(ent.Comp.MissLocString), ent, thrower);
            }
            return;
        }

        if (!_containerSystem.Insert(args.Thrown, container))
            return;

        if (feedback)
            _audio.PlayPredicted(ent.Comp.InsertSound, ent, thrower);

        if (args.Component.Thrower != null)
            _adminLogger.Add(LogType.Landed, LogImpact.Low, $"{ToPrettyString(args.Thrown)} thrown by {ToPrettyString(args.Component.Thrower.Value):player} landed in {ToPrettyString(ent)}");
    }
}

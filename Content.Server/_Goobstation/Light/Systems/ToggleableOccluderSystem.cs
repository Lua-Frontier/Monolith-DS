using Content.Server.DeviceLinking.Systems;
using Content.Server._Goobstation.Light.Components;
using Content.Shared.DeviceLinking.Events;
using Robust.Shared.Audio.Systems; // LuaM

namespace Content.Server._Goobstation.Light.Systems;

/// <summary>
///     Handles the logic between signals and toggling OccluderComponent, early upstream merge of #30743
/// </summary>
public sealed partial class ToggleableOccluderSystem : EntitySystem
{
    [Dependency] private DeviceLinkSystem _signalSystem = default!;
    [Dependency] private OccluderSystem _occluder = default!;
    [Dependency] private SharedAudioSystem _audio = default!; // LuaM

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ToggleableOccluderComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<ToggleableOccluderComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, ToggleableOccluderComponent comp, ComponentInit args)
    {
        _signalSystem.EnsureSinkPorts(uid, comp.OnPort, comp.OffPort, comp.TogglePort);
    }

    private void OnSignalReceived(EntityUid uid, ToggleableOccluderComponent comp, ref SignalReceivedEvent args)
    {
        if (!TryComp<OccluderComponent>(uid, out var occluder))
            return;

        if (args.Port == comp.OffPort)
            SetState(uid, false, occluder);
        else if (args.Port == comp.OnPort)
            SetState(uid, true, occluder);
        else if (args.Port == comp.TogglePort)
            ToggleState(uid, occluder);
    }

    public void ToggleState(EntityUid uid, OccluderComponent? occluder = null)
    {
        if (!Resolve(uid, ref occluder))
            return;

        _occluder.SetEnabled(uid, !occluder.Enabled);
        PlayToggleSound(uid); // LuaM
    }

    public void SetState(EntityUid uid, bool state, OccluderComponent? occluder = null)
    {
        if (!Resolve(uid, ref occluder))
            return;

        if (occluder.Enabled == state) // LuaM
            return; // LuaM

        _occluder.SetEnabled(uid, state);
        PlayToggleSound(uid); // LuaM
    }

    // LuaM-start
    private void PlayToggleSound(EntityUid uid)
    {
        if (TryComp<ToggleableOccluderComponent>(uid, out var comp) && comp.ToggleSound != null)
            _audio.PlayPvs(comp.ToggleSound, uid);
    }
    // LuaM-end

}

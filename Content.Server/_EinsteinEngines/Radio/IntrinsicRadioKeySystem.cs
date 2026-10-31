using Content.Server.Radio.Components;
using Content.Shared._LuaM.Radio; // LuaM
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server._EinsteinEngines.Radio;

public sealed class IntrinsicRadioKeySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IntrinsicRadioTransmitterComponent, EncryptionChannelsChangedEvent>(OnTransmitterChannelsChanged);
        SubscribeLocalEvent<ActiveRadioComponent, EncryptionChannelsChangedEvent>(OnReceiverChannelsChanged);
    }

    private void OnTransmitterChannelsChanged(EntityUid uid, IntrinsicRadioTransmitterComponent component, EncryptionChannelsChangedEvent args)
    {
        UpdateChannels(uid, args.Component, ref component.Channels);
    }

    private void OnReceiverChannelsChanged(EntityUid uid, ActiveRadioComponent component, EncryptionChannelsChangedEvent args)
    {
        UpdateChannels(uid, args.Component, ref component.Channels);
    }

    // LuaM start: re-applies the built-in radio channels, e.g. after channels were switched off in the channels UI
    public void Refresh(EntityUid uid)
    {
        if (!TryComp<EncryptionKeyHolderComponent>(uid, out var keyHolder))
            return;

        if (TryComp<IntrinsicRadioTransmitterComponent>(uid, out var transmitter))
            UpdateChannels(uid, keyHolder, ref transmitter.Channels);

        if (TryComp<ActiveRadioComponent>(uid, out var receiver))
            UpdateChannels(uid, keyHolder, ref receiver.Channels);
    }
    // LuaM end

    private void UpdateChannels(EntityUid uid, EncryptionKeyHolderComponent keyHolderComp, ref HashSet<ProtoId<RadioChannelPrototype>> channels) // LuaM: _ > uid
    {
        channels.Clear();
        channels.UnionWith(keyHolderComp.Channels.Select(c => new ProtoId<RadioChannelPrototype>(c)));

        // LuaM: channels switched off in the channels UI are neither heard nor transmitted on
        if (TryComp<HeadsetChannelToggleComponent>(uid, out var toggle))
            channels.ExceptWith(toggle.DisabledChannels.Select(c => new ProtoId<RadioChannelPrototype>(c)));
    }
}

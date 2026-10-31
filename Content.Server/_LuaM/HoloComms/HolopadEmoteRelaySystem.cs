using System.Linq;
using Content.Server._LuaM.Chat;
using Content.Server._LuaM.Telephone;
using Content.Server.Chat.Systems;
using Content.Server.Speech.Components;
using Content.Shared.Chat;
using Content.Shared.Holopad;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Telephone;

namespace Content.Server._LuaM.HoloComms;

/// <summary>
/// Relays the emotes of everyone a holopad in a call can hear, the typed ones included, through the holograms on the other end.
/// The holopads used to relay only the picked emotes of the one shown on the hologram.
/// </summary>
public sealed partial class HolopadEmoteRelaySystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private TelephoneRelayChatSystem _relayChat = default!;

    // A ship line can see the same emote through several points, the other side gets it once
    private readonly HashSet<EntityUid> _relayedTo = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntityEmoteSentEvent>(OnEmoteSent);
    }

    private void OnEmoteSent(ref EntityEmoteSentEvent args)
    {
        // Same as speech: holograms and machines have no mind, so a relayed emote never comes back
        if (!HasComp<MindContainerComponent>(args.Source))
            return;

        var sourceXform = Transform(args.Source);
        var sourcePos = _xform.GetWorldPosition(sourceXform);
        string? name = null;

        _relayedTo.Clear();

        // Only the telephones with the microphone on are in a call, there are few of them
        var query = EntityQueryEnumerator<ActiveListenerComponent, HolopadComponent, TelephoneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var listener, out var holopad, out var telephone, out var xform))
        {
            if (uid == args.Source || telephone.Muted || telephone.CurrentState != TelephoneState.InCall)
                continue;

            // The one shown through the holopad is heard wherever they are, like the AI
            if (holopad.User?.Owner != args.Source)
            {
                if (xform.MapID != sourceXform.MapID ||
                    (_xform.GetWorldPosition(xform) - sourcePos).LengthSquared() > listener.Range * listener.Range ||
                    !_interaction.InRangeUnobstructed(args.Source, uid, 0f))
                {
                    continue;
                }
            }

            name ??= Loc.GetString("holopad-hologram-name", ("name", Identity.Entity(args.Source, EntityManager)));

            // The loud ones first, the same as speech
            foreach (var receiver in telephone.LinkedTelephones.OrderByDescending(r => r.Comp.SpeakerVolume == TelephoneVolume.Speak))
            {
                if (!TryComp<HolopadComponent>(receiver, out var receiverHolopad) ||
                    receiverHolopad.Hologram is not { } hologram ||
                    !_relayedTo.Add(receiver.Owner))
                {
                    continue;
                }

                // Same as speech, only broadcasts stay out of the chat
                var range = telephone.LinkedTelephones.Count > 1 && receiver.Comp.Muted
                    ? ChatTransmitRange.HideChat
                    : ChatTransmitRange.GhostRangeLimit;

                // Forced, if the user can do it the hologram can too. Of a few points near each other only the first shows it
                if (_relayChat.TrySay(hologram, args.Source, args.Action))
                {
                    using (_relayChat.Relay(hologram, args.Source, args.Action))
                    {
                        _chat.TrySendInGameICMessage(hologram, args.Action, InGameICChatType.Emote, range,
                            hideLog: true, nameOverride: name, checkRadioPrefix: false, ignoreActionBlocker: true);
                    }
                }

                var relayedEv = new HolopadEmoteRelayedEvent(args.Source, args.Action);
                RaiseLocalEvent(receiver, ref relayedEv);
            }
        }
    }
}

/// <summary>
/// Raised on a holopad after its hologram repeated the emote of somebody on the other end.
/// </summary>
[ByRefEvent]
public readonly record struct HolopadEmoteRelayedEvent(EntityUid Source, string Action);

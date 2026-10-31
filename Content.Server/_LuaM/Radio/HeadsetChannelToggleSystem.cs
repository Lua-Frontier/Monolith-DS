using System.Linq;
using Content.Server._EinsteinEngines.Radio;
using Content.Server.Radio.EntitySystems;
using Content.Shared._LuaM.Radio;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._LuaM.Radio;

public sealed partial class HeadsetChannelToggleSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private HeadsetSystem _headset = default!;
    [Dependency] private IntrinsicRadioKeySystem _intrinsicKeys = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeadsetChannelToggleComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<HeadsetChannelToggleComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<HeadsetChannelToggleComponent, EncryptionChannelsChangedEvent>(OnChannelsChanged);
        SubscribeLocalEvent<HeadsetTunableRadioComponent, MapInitEvent>(OnTunableMapInit);
        SubscribeLocalEvent<HeadsetTunableRadioComponent, ExaminedEvent>(OnTunableExamined);

        Subs.BuiEvents<HeadsetChannelToggleComponent>(HeadsetChannelsUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<HeadsetChannelToggleMessage>(OnToggleMessage);
            subs.Event<HeadsetSetFrequencyMessage>(OnSetFrequencyMessage);
        });
    }

    /// <summary>
    /// Whether the given channel was switched off on this headset.
    /// </summary>
    public bool IsChannelDisabled(EntityUid headset, string channel)
    {
        return TryComp<HeadsetChannelToggleComponent>(headset, out var toggle) && toggle.DisabledChannels.Contains(channel);
    }

    private void OnUseInHand(Entity<HeadsetChannelToggleComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = _ui.TryOpenUi(ent.Owner, HeadsetChannelsUiKey.Key, args.User);
    }

    private void OnGetVerbs(Entity<HeadsetChannelToggleComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // A built-in radio (e.g. an IPC) is tuned only by its owner, a headset by anyone holding it.
        if (!IsHeadsetItem(ent) && args.User != ent.Owner)
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Act = () => _ui.TryOpenUi(ent.Owner, HeadsetChannelsUiKey.Key, user),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Text = Loc.GetString("headset-channels-verb"),
        });
    }

    private void OnChannelsChanged(Entity<HeadsetChannelToggleComponent> ent, ref EncryptionChannelsChangedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnUiOpened(Entity<HeadsetChannelToggleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnToggleMessage(Entity<HeadsetChannelToggleComponent> ent, ref HeadsetChannelToggleMessage args)
    {
        if (!TryComp<EncryptionKeyHolderComponent>(ent, out var keys) || !keys.Channels.Contains(args.Channel))
            return;

        var changed = args.Enabled
            ? ent.Comp.DisabledChannels.Remove(args.Channel)
            : ent.Comp.DisabledChannels.Add(args.Channel);

        if (!changed)
            return;

        Dirty(ent);
        _headset.RefreshRadioChannels(ent);
        _intrinsicKeys.Refresh(ent);
        UpdateUi(ent);
    }

    private void OnTunableMapInit(Entity<HeadsetTunableRadioComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Frequency != 0)
            return;

        if (ent.Comp.RandomFrequency)
            ent.Comp.Frequency = _random.Next(ent.Comp.MinFrequency, ent.Comp.MaxFrequency + 1);
        else if (_proto.TryIndex(ent.Comp.Channel, out var channel))
            ent.Comp.Frequency = Math.Clamp(channel.Frequency, ent.Comp.MinFrequency, ent.Comp.MaxFrequency);
        else
            ent.Comp.Frequency = ent.Comp.MinFrequency;

        Dirty(ent);
    }

    private void OnTunableExamined(Entity<HeadsetTunableRadioComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        // the frequency of a built-in radio is only known to its owner
        if (!IsHeadsetItem(ent) && args.Examiner != ent.Owner)
            return;

        args.PushMarkup(Loc.GetString("headset-tunable-radio-examine", ("frequency", ent.Comp.Frequency)));
    }

    private void OnSetFrequencyMessage(Entity<HeadsetChannelToggleComponent> ent, ref HeadsetSetFrequencyMessage args)
    {
        if (!TryComp<HeadsetTunableRadioComponent>(ent, out var tunable))
            return;

        if (args.Frequency < tunable.MinFrequency || args.Frequency > tunable.MaxFrequency)
        {
            UpdateUi(ent); // put the valid frequency back into the UI
            return;
        }

        tunable.Frequency = args.Frequency;
        Dirty(ent.Owner, tunable);
        UpdateUi(ent);
    }

    private bool IsHeadsetItem(EntityUid uid)
    {
        return HasComp<ItemComponent>(uid);
    }

    private void UpdateUi(Entity<HeadsetChannelToggleComponent> ent)
    {
        if (!TryComp<EncryptionKeyHolderComponent>(ent, out var keys))
            return;

        TryComp<HeadsetTunableRadioComponent>(ent, out var tunable);

        var state = new HeadsetChannelsBoundUserInterfaceState(
            keys.Channels.OrderBy(c => c).ToList(),
            new HashSet<string>(ent.Comp.DisabledChannels),
            tunable?.Frequency,
            tunable?.MinFrequency ?? 0,
            tunable?.MaxFrequency ?? 0);

        _ui.SetUiState(ent.Owner, HeadsetChannelsUiKey.Key, state);
    }
}

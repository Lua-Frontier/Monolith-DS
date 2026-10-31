using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Server._EinsteinEngines.Language;
using Content.Server._LuaM.Shuttles;
using Content.Server.Administration.Logs;
using Content.Server.Holopad;
using Content.Server.Popups;
using Content.Server.Telephone;
using Content.Shared._LuaM.HoloComms;
using Content.Shared._LuaM.Shuttles.Events;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Holopad;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power;
using Content.Shared.Shuttles.Components;
using Content.Shared.Telephone;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.HoloComms;

/// <summary>
/// The comms window of the shuttle consoles. The calls go through a hidden holopad attached to the console,
/// the window shows the other side through its hologram, and the hologram of the other side shows the one closest to the console.
/// </summary>
public sealed partial class HoloCommsConsoleSystem : EntitySystem
{
    [Dependency] private TelephoneSystem _telephone = default!;
    [Dependency] private HolopadSystem _holopad = default!;
    [Dependency] private ShipCommsLineSystem _line = default!;
    [Dependency] private ShuttleConsoleUiGuardSystem _consoleGuard = default!;
    [Dependency] private RadarIdentificationSystem _identification = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private LanguageSystem _language = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan ShownUserInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan ContactsInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How much closer someone else has to stand before the hologram switches over, so it does not jump between two people.
    /// </summary>
    private const float ShownUserHysteresis = 0.75f;

    // Only the consoles somebody looks at and the modules in a call need work every so often
    private readonly HashSet<EntityUid> _openConsoles = new();
    private readonly HashSet<EntityUid> _activeModules = new();
    private readonly List<EntityUid> _buffer = new();
    private readonly HashSet<Entity<MindContainerComponent>> _nearby = new();
    private readonly List<HoloCommsContact> _contacts = new();

    private TimeSpan _nextShownUserUpdate;
    private TimeSpan _nextContactsUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HoloCommsConsoleComponent, MapInitEvent>(OnConsoleMapInit);
        SubscribeLocalEvent<HoloCommsConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
        SubscribeLocalEvent<HoloCommsConsoleComponent, PowerChangedEvent>(OnConsolePowerChanged);
        SubscribeLocalEvent<HoloCommsConsoleComponent, BoundUserInterfaceMessageAttempt>(OnUiMessageAttempt);

        Subs.BuiEvents<HoloCommsConsoleComponent>(ShuttleConsoleUiKey.Key, subs =>
        {
            subs.Event<HoloCommsToggleWindowMessage>(OnToggleWindow);
            subs.Event<ShuttleRadarCallMessage>(OnRadarCall);
        });

        Subs.BuiEvents<HoloCommsConsoleComponent>(HoloCommsUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
            subs.Event<HoloCommsCallMessage>(OnCall);
            subs.Event<HoloCommsAnswerMessage>(OnAnswer);
            subs.Event<HoloCommsHangUpMessage>(OnHangUp);
            subs.Event<ShipCommsSetDoNotDisturbMessage>(OnSetDoNotDisturb);
        });

        SubscribeLocalEvent<HoloCommsModuleComponent, ComponentShutdown>(OnModuleShutdown);
        SubscribeLocalEvent<HoloCommsModuleComponent, TelephoneStateChangeEvent>(OnModuleStateChange);
        SubscribeLocalEvent<HoloCommsModuleComponent, TelephoneCallCommencedEvent>(OnModuleCallCommenced,
            after: [typeof(HolopadSystem), typeof(ShipCommsLineSystem)]);
        SubscribeLocalEvent<HoloCommsModuleComponent, TelephoneMessageReceivedEvent>(OnModuleMessageReceived);
        SubscribeLocalEvent<HoloCommsModuleComponent, HolopadEmoteRelayedEvent>(OnModuleEmoteRelayed);

        SubscribeLocalEvent<ShipCommsLineChangedEvent>(OnLineChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        if (now >= _nextShownUserUpdate)
        {
            _nextShownUserUpdate = now + ShownUserInterval;

            _buffer.Clear();
            _buffer.AddRange(_activeModules);
            foreach (var module in _buffer)
            {
                UpdateShownUser(module);
            }
        }

        if (now >= _nextContactsUpdate)
        {
            _nextContactsUpdate = now + ContactsInterval;

            _buffer.Clear();
            _buffer.AddRange(_openConsoles);
            foreach (var console in _buffer)
            {
                if (TryComp<HoloCommsConsoleComponent>(console, out var comp))
                    UpdateUi((console, comp));
            }
        }
    }

    #region Console

    private void OnConsoleMapInit(Entity<HoloCommsConsoleComponent> ent, ref MapInitEvent args)
    {
        EnsureModule(ent);
    }

    private void OnConsoleShutdown(Entity<HoloCommsConsoleComponent> ent, ref ComponentShutdown args)
    {
        _openConsoles.Remove(ent);

        // The module goes with the console anyway, this covers the component being taken off alone
        if (ent.Comp.Module is { } module && !TerminatingOrDeleted(module))
            QueueDel(module);
    }

    private void OnConsolePowerChanged(Entity<HoloCommsConsoleComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered && ent.Comp.Module is { } module && TryComp<TelephoneComponent>(module, out var telephone))
            _telephone.TerminateTelephoneCalls((module, telephone));

        UpdateUi(ent);
    }

    /// <summary>
    /// The module of the console, made if it is missing. A saved ship brings it along as a child of the console.
    /// </summary>
    public EntityUid? EnsureModule(Entity<HoloCommsConsoleComponent> ent)
    {
        if (ent.Comp.Module is { } existing && !TerminatingOrDeleted(existing) && HasComp<HoloCommsModuleComponent>(existing))
            return existing;

        // Mapping keeps maps uninitialized, a module made there would be saved into the map
        if (TerminatingOrDeleted(ent) || LifeStage(ent) < EntityLifeStage.MapInitialized)
            return null;

        var children = Transform(ent).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!TryComp<HoloCommsModuleComponent>(child, out var found))
                continue;

            found.Console = ent;
            SetModule(ent, child);
            return child;
        }

        var module = SpawnAttachedTo(ent.Comp.ModulePrototype, new EntityCoordinates(ent, Vector2.Zero));
        EnsureComp<HoloCommsModuleComponent>(module).Console = ent;
        SetModule(ent, module);
        return module;
    }

    private void SetModule(Entity<HoloCommsConsoleComponent> ent, EntityUid module)
    {
        ent.Comp.Module = module;
        Dirty(ent);
    }

    private bool TryGetModule(Entity<HoloCommsConsoleComponent> ent,
        [NotNullWhen(true)] out Entity<HolopadComponent, TelephoneComponent>? module)
    {
        module = null;

        if (EnsureModule(ent) is not { } uid ||
            !TryComp<HolopadComponent>(uid, out var holopad) ||
            !TryComp<TelephoneComponent>(uid, out var telephone))
        {
            return false;
        }

        module = (uid, holopad, telephone);
        return true;
    }

    #endregion

    #region Window

    private void OnToggleWindow(Entity<HoloCommsConsoleComponent> ent, ref HoloCommsToggleWindowMessage args)
    {
        _ui.TryToggleUi(ent.Owner, HoloCommsUiKey.Key, args.Actor);
    }

    // -- окно связи открывается только из окна консоли, с запертой консоли не звонят
    private void OnUiMessageAttempt(EntityUid uid, HoloCommsConsoleComponent component, BoundUserInterfaceMessageAttempt args)
    {
        if (!HoloCommsUiKey.Key.Equals(args.UiKey))
            return;

        var allowed = args.Message is OpenBoundInterfaceMessage
            ? _ui.IsUiOpen(uid, ShuttleConsoleUiKey.Key, args.Actor)
            : !_consoleGuard.IsLocked(uid);

        if (!allowed)
            args.Cancel();
    }

    private void OnUiOpened(Entity<HoloCommsConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        _openConsoles.Add(ent);
        UpdateUi(ent);
    }

    private void OnUiClosed(Entity<HoloCommsConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!_ui.IsUiOpen(ent.Owner, HoloCommsUiKey.Key))
            _openConsoles.Remove(ent);
    }

    private void OnCall(Entity<HoloCommsConsoleComponent> ent, ref HoloCommsCallMessage args)
    {
        if (TryGetEntity(args.Target, out var target))
            TryCall(ent, args.Actor, target.Value, null);
    }

    private void OnRadarCall(Entity<HoloCommsConsoleComponent> ent, ref ShuttleRadarCallMessage args)
    {
        if (!TryGetEntity(args.Target, out var target) ||
            Transform(target.Value).GridUid is not { } grid ||
            !_identification.CanTarget(ent, grid, args.Actor) ||
            !_ui.TryOpenUi(ent.Owner, HoloCommsUiKey.Key, args.Actor))
        {
            return;
        }

        TryCall(ent, args.Actor, target.Value, args.Actor);
    }

    public void GetRadarCalls(Entity<HoloCommsConsoleComponent> ent, EntityUid grid, EntityUid actor, List<HoloCommsContact> calls)
    {
        if (!TryGetModule(ent, out var module))
            return;

        var source = new Entity<TelephoneComponent>(module.Value.Owner, module.Value.Comp2);
        _line.GetGridContacts(source, grid, actor, calls);

        if (_telephone.IsTelephonePowered(source) && !_telephone.IsTelephoneEngaged(source))
            return;

        for (var i = 0; i < calls.Count; i++)
        {
            calls[i] = calls[i] with { Busy = true };
        }
    }

    private void TryCall(Entity<HoloCommsConsoleComponent> ent, EntityUid actor, EntityUid target, EntityUid? viewer)
    {
        if (!TryGetModule(ent, out var module))
            return;

        var (uid, holopad, telephone) = module.Value;
        var source = new Entity<TelephoneComponent>(uid, telephone);

        if (_telephone.IsTelephoneEngaged(source) || !_telephone.IsTelephonePowered(source))
            return;

        target = _line.ResolveCallTarget(uid, target);

        if (!TryComp<TelephoneComponent>(target, out var targetTelephone) || !HasComp<HolopadComponent>(target))
            return;

        var receiver = new Entity<TelephoneComponent>(target, targetTelephone);

        if (!_line.CanCall(source, receiver, viewer))
            return;

        if (_line.IsDoNotDisturb(receiver))
        {
            _popup.PopupEntity(Loc.GetString("holo-comms-popup-do-not-disturb"), ent, actor);
            return;
        }

        if (!_telephone.IsSourceAbleToReachReceiver(source, receiver) || _telephone.IsTelephoneEngaged(receiver))
        {
            _popup.PopupEntity(Loc.GetString("holo-comms-popup-unavailable"), ent, actor);
            return;
        }

        _holopad.SetHolopadUser((uid, holopad), actor);
        _telephone.CallTelephone(source, receiver, actor);

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(actor):user} called {ToPrettyString(receiver):target} from the comms of {ToPrettyString(ent):console}");
    }

    private void OnAnswer(Entity<HoloCommsConsoleComponent> ent, ref HoloCommsAnswerMessage args)
    {
        if (!TryGetModule(ent, out var module))
            return;

        var (uid, holopad, telephone) = module.Value;
        if (telephone.CurrentState != TelephoneState.Ringing)
            return;

        _holopad.SetHolopadUser((uid, holopad), args.Actor);
        _telephone.AnswerTelephone((uid, telephone), args.Actor);
    }

    private void OnHangUp(Entity<HoloCommsConsoleComponent> ent, ref HoloCommsHangUpMessage args)
    {
        if (TryGetModule(ent, out var module))
            _line.HangUp((module.Value.Owner, module.Value.Comp2));
    }

    private void OnSetDoNotDisturb(Entity<HoloCommsConsoleComponent> ent, ref ShipCommsSetDoNotDisturbMessage args)
    {
        if (TryGetModule(ent, out var module))
            _line.SetDoNotDisturb(module.Value, args.Enabled);
    }

    private void OnLineChanged(ref ShipCommsLineChangedEvent args)
    {
        foreach (var console in _openConsoles)
        {
            if (Transform(console).GridUid == args.Grid && TryComp<HoloCommsConsoleComponent>(console, out var comp))
                UpdateUi((console, comp));
        }
    }

    private void UpdateUi(Entity<HoloCommsConsoleComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, HoloCommsUiKey.Key) || !TryGetModule(ent, out var module))
            return;

        var (uid, holopad, telephone) = module.Value;
        var source = new Entity<TelephoneComponent>(uid, telephone);

        var phase = !_telephone.IsTelephonePowered(source)
            ? HoloCommsPhase.Unpowered
            : telephone.CurrentState switch
            {
                TelephoneState.Calling => HoloCommsPhase.Calling,
                TelephoneState.Ringing => HoloCommsPhase.Ringing,
                TelephoneState.InCall => HoloCommsPhase.InCall,
                TelephoneState.EndingCall => HoloCommsPhase.Ending,
                _ => HoloCommsPhase.Idle,
            };

        string? remoteName = null;
        foreach (var linked in telephone.LinkedTelephones)
        {
            remoteName = _line.GetRemoteName(uid, linked);
            break;
        }

        string? callerName = null;
        if (phase == HoloCommsPhase.Ringing)
        {
            var (name, job, _) = telephone.LastCallerId;
            callerName = string.IsNullOrEmpty(job) ? name : Loc.GetString("holo-comms-caller-with-job", ("name", name ?? string.Empty), ("job", job));
        }

        // The contacts are only of use while there is no call, they are the heaviest part of the state
        _contacts.Clear();
        if (phase == HoloCommsPhase.Idle)
            _line.GetContacts(source, _contacts);

        var state = new HoloCommsBoundUserInterfaceState(
            phase,
            remoteName,
            callerName,
            GetNetEntity(holopad.Hologram?.Owner),
            _line.IsDoNotDisturb(uid),
            new List<HoloCommsContact>(_contacts));

        _ui.SetUiState(ent.Owner, HoloCommsUiKey.Key, state);
    }

    #endregion

    #region Module

    private void OnModuleShutdown(Entity<HoloCommsModuleComponent> ent, ref ComponentShutdown args)
    {
        _activeModules.Remove(ent);
    }

    private void OnModuleStateChange(Entity<HoloCommsModuleComponent> ent, ref TelephoneStateChangeEvent args)
    {
        if (args.NewState is TelephoneState.Calling or TelephoneState.InCall)
            _activeModules.Add(ent);
        else
            _activeModules.Remove(ent);

        UpdateRemoteName(ent, args.NewState);
        UpdateModuleConsoleUi(ent);
    }

    private void UpdateRemoteName(Entity<HoloCommsModuleComponent> ent, TelephoneState state)
    {
        if (ent.Comp.Console is not { } console || !TryComp<HoloCommsConsoleComponent>(console, out var comp))
            return;

        string? remote = null;
        if (state is TelephoneState.Calling or TelephoneState.Ringing or TelephoneState.InCall &&
            TryComp<TelephoneComponent>(ent, out var telephone))
        {
            foreach (var linked in telephone.LinkedTelephones)
            {
                remote = _line.GetRemoteName(ent, linked);
                break;
            }
        }

        if (comp.RemoteName == remote)
            return;

        comp.RemoteName = remote;
        Dirty(console, comp);
    }

    private void OnModuleCallCommenced(Entity<HoloCommsModuleComponent> ent, ref TelephoneCallCommencedEvent args)
    {
        // The hologram is made by now, the window can show it
        UpdateModuleConsoleUi(ent);
        UpdateShownUser(ent);
    }

    private void OnModuleMessageReceived(Entity<HoloCommsModuleComponent> ent, ref TelephoneMessageReceivedEvent args)
    {
        if (!TryGetShownSpeaker(ent, args.MessageSource, out var console, out var hologram))
            return;

        var nameEv = new TransformSpeakerNameEvent(args.MessageSource, Name(args.MessageSource));
        RaiseLocalEvent(args.MessageSource, nameEv);

        // The window shows what the hologram says over the console, in the same language
        var language = args.Language ?? _language.GetLanguage(hologram.Value);

        foreach (var actor in _ui.GetActors(console.Value, HoloCommsUiKey.Key))
        {
            var text = _language.CanUnderstand(actor, language.ID)
                ? args.Message
                : _language.ObfuscateSpeech(args.Message, language);

            _ui.ServerSendUiMessage(console.Value, HoloCommsUiKey.Key, new HoloCommsLineMessage(nameEv.VoiceName, text, false), actor);
        }
    }

    private void OnModuleEmoteRelayed(Entity<HoloCommsModuleComponent> ent, ref HolopadEmoteRelayedEvent args)
    {
        if (!TryGetShownSpeaker(ent, args.Source, out var console, out _))
            return;

        var name = Identity.Name(args.Source, EntityManager);
        _ui.ServerSendUiMessage(console.Value, HoloCommsUiKey.Key, new HoloCommsLineMessage(name, args.Action, true));
    }

    /// <summary>
    /// Only the one shown on the hologram talks through the window, everyone else is heard over the console.
    /// </summary>
    private bool TryGetShownSpeaker(Entity<HoloCommsModuleComponent> ent,
        EntityUid speaker,
        [NotNullWhen(true)] out EntityUid? console,
        [NotNullWhen(true)] out EntityUid? hologram)
    {
        console = ent.Comp.Console;
        hologram = null;

        if (console == null || !_ui.IsUiOpen(console.Value, HoloCommsUiKey.Key))
            return false;

        if (!TryComp<HolopadComponent>(ent, out var holopad) || holopad.Hologram is not { } shown)
            return false;

        if (shown.Comp.LinkedEntity != speaker)
            return false;

        hologram = shown.Owner;
        return true;
    }

    private void UpdateModuleConsoleUi(Entity<HoloCommsModuleComponent> ent)
    {
        if (ent.Comp.Console is { } console && TryComp<HoloCommsConsoleComponent>(console, out var comp))
            UpdateUi((console, comp));
    }

    /// <summary>
    /// The other side sees the one standing closest to the console.
    /// </summary>
    private void UpdateShownUser(EntityUid module)
    {
        if (!TryComp<HolopadComponent>(module, out var holopad) ||
            !TryComp<TelephoneComponent>(module, out var telephone) ||
            !_telephone.IsTelephoneEngaged((module, telephone)))
        {
            return;
        }

        var origin = _xform.GetMapCoordinates(module);
        var current = holopad.User?.Owner;
        float? currentDistance = null;
        EntityUid? best = null;
        var bestDistance = float.MaxValue;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(origin, telephone.ListeningRange, _nearby, LookupFlags.Dynamic | LookupFlags.Sundries);

        foreach (var candidate in _nearby)
        {
            if (!IsShowable(candidate, module))
                continue;

            var distance = (_xform.GetWorldPosition(candidate) - origin.Position).Length();

            if (candidate.Owner == current)
                currentDistance = distance;

            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        // The one shown stays while still at the console, unless someone else stands clearly closer
        if (currentDistance <= bestDistance + ShownUserHysteresis)
            return;

        if (best != current)
            _holopad.SetHolopadUser((module, holopad), best);
    }

    private bool IsShowable(Entity<MindContainerComponent> candidate, EntityUid module)
    {
        if (!candidate.Comp.HasMind ||
            !HasComp<MobStateComponent>(candidate) ||
            _mobState.IsDead(candidate) ||
            HasComp<GhostComponent>(candidate) ||
            HasComp<HolopadHologramComponent>(candidate))
        {
            return false;
        }

        // Same as the holopad microphone, somebody behind a wall is not at the console
        return _interaction.InRangeUnobstructed(candidate.Owner, module, 0f);
    }

    #endregion
}

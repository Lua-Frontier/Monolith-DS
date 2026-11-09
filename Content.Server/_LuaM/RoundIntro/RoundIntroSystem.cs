using Content.Shared._LuaM.RoundIntro;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Server.GameStates;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.RoundIntro;

public sealed partial class RoundIntroSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;

    private const int MaxCoworkers = 12;
    private static readonly TimeSpan OverrideDuration = TimeSpan.FromSeconds(20);

    // -- у вольных должностей (наёмники, контрактники, пилоты, борги) заставка без списка коллег
    private static readonly HashSet<ProtoId<DepartmentPrototype>> IgnoredDepartments =
    [
        "Civilian",
        "PMC",
        "Silicon",
        "Command",
    ];

    private readonly HashSet<ICommonSession> _expected = new();
    private readonly List<PendingIntro> _pending = new();
    private readonly List<ResolvedIntro> _resolved = new();
    private readonly List<TimedOverride> _overrides = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _expected.Clear();
        _pending.Clear();
        _overrides.Clear();
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if (ev.JobId == null || !_proto.HasIndex<JobPrototype>(ev.JobId))
            return;

        _expected.Add(ev.Player);
        RaiseNetworkEvent(new RoundIntroExpectEvent(), ev.Player.Channel);
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!_expected.Remove(ev.Player) || ev.JobId == null || !_proto.HasIndex<JobPrototype>(ev.JobId))
            return;

        _pending.Add(new PendingIntro(ev.Player, ev.JobId, ev.Station, ev.LateJoin));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _expected.Clear();

        if (_pending.Count > 0)
        {
            SendIntros();
            _pending.Clear();
            _resolved.Clear();
        }

        if (_overrides.Count == 0)
            return;

        var now = _timing.CurTime;
        for (var i = _overrides.Count - 1; i >= 0; i--)
        {
            var entry = _overrides[i];
            if (entry.EndTime > now)
                continue;

            if (!TerminatingOrDeleted(entry.Entity))
                _pvsOverride.RemoveSessionOverride(entry.Entity, entry.Session);

            _overrides.RemoveAt(i);
        }
    }

    private void SendIntros()
    {
        foreach (var pending in _pending)
        {
            if (pending.Session.Status != SessionStatus.InGame
                || pending.Session.AttachedEntity is not { } current
                || TerminatingOrDeleted(current))
                continue;

            _resolved.Add(new ResolvedIntro(pending, current, GetDepartments(pending.Job)));
        }

        var endTime = _timing.CurTime + OverrideDuration;

        foreach (var intro in _resolved)
        {
            var pending = intro.Pending;
            var station = Exists(pending.Station) ? Name(pending.Station) : null;

            RaiseNetworkEvent(new RoundIntroEvent(GetNetEntity(intro.Entity), Name(intro.Entity), pending.Job, station),
                pending.Session.Channel);

            if (!pending.LateJoin && intro.Departments.Count > 0)
                SendCoworkers(pending.Session, GetCoworkers(intro), endTime);
        }
    }

    private void SendCoworkers(ICommonSession session, List<(EntityUid Entity, RoundIntroCoworker Info)> coworkers, TimeSpan endTime)
    {
        if (coworkers.Count == 0)
            return;

        var infos = new List<RoundIntroCoworker>(coworkers.Count);
        foreach (var (entity, info) in coworkers)
        {
            infos.Add(info);
            _pvsOverride.AddSessionOverride(entity, session);
            _overrides.Add(new TimedOverride(entity, session, endTime));
        }

        RaiseNetworkEvent(new RoundIntroCoworkersEvent(infos), session.Channel);
    }

    private List<(EntityUid, RoundIntroCoworker)> GetCoworkers(ResolvedIntro intro)
    {
        var coworkers = new List<(EntityUid, RoundIntroCoworker)>();

        foreach (var other in _resolved)
        {
            if (coworkers.Count >= MaxCoworkers)
                break;

            if (other.Pending.Session == intro.Pending.Session
                || other.Pending.LateJoin
                || other.Pending.Station != intro.Pending.Station
                || !SharesDepartment(intro.Departments, other.Pending.Job))
                continue;

            coworkers.Add((other.Entity, new RoundIntroCoworker(GetNetEntity(other.Entity), Name(other.Entity), other.Pending.Job)));
        }

        return coworkers;
    }

    private List<DepartmentPrototype> GetDepartments(ProtoId<JobPrototype> job)
    {
        var departments = new List<DepartmentPrototype>();
        foreach (var department in _proto.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (!IgnoredDepartments.Contains(department.ID) && department.Roles.Contains(job))
                departments.Add(department);
        }

        return departments;
    }

    private static bool SharesDepartment(List<DepartmentPrototype> departments, ProtoId<JobPrototype> job)
    {
        foreach (var department in departments)
        {
            if (department.Roles.Contains(job))
                return true;
        }

        return false;
    }

    private readonly record struct PendingIntro(ICommonSession Session, ProtoId<JobPrototype> Job, EntityUid Station, bool LateJoin);

    private readonly record struct ResolvedIntro(PendingIntro Pending, EntityUid Entity, List<DepartmentPrototype> Departments);

    private readonly record struct TimedOverride(EntityUid Entity, ICommonSession Session, TimeSpan EndTime);
}

using Content.Server._NF.Station.Components;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._LuaM.Administration;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.Administration;

/// <summary>
/// Lets admins open and close the job slots of the faction stations from the admin panel.
/// </summary>
public sealed partial class AdminStationJobsSystem : EntitySystem
{
    private const AdminFlags RequiredFlag = AdminFlags.Admin;

    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private StationJobsSystem _jobs = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<AdminStationJobsRequestEvent>(OnRequest);
        SubscribeNetworkEvent<AdminStationJobsChangeEvent>(OnChange);
    }

    private void OnRequest(AdminStationJobsRequestEvent ev, EntitySessionEventArgs args)
    {
        if (_admin.HasAdminFlag(args.SenderSession, RequiredFlag))
            SendState(args.SenderSession, null);
    }

    private void OnChange(AdminStationJobsChangeEvent ev, EntitySessionEventArgs args)
    {
        var player = args.SenderSession;
        if (!_admin.HasAdminFlag(player, RequiredFlag))
            return;

        SendState(player, TryChange(player, ev));
    }

    /// <summary>
    /// Changes the slots, returns why it failed or null if it went through.
    /// </summary>
    private string? TryChange(ICommonSession player, AdminStationJobsChangeEvent ev)
    {
        if (ev.Amount is not (1 or -1)
            || !TryGetEntity(ev.Station, out var station)
            || !TryComp<StationJobsComponent>(station, out var stationJobs)
            || !_proto.TryIndex<JobPrototype>(ev.Job, out var job)
            || AdminStationJobFactions.FromCompany(job.AssignedCompany.Id) == null
            || !_jobs.GetJobs(station.Value, stationJobs).TryGetValue(job.ID, out var slots)
            || slots is not >= 0)
        {
            return Loc.GetString("luam-admin-jobs-error-unavailable");
        }

        if (!_jobs.TryAdjustJobSlot(station.Value, job.ID, ev.Amount, false, true, stationJobs))
            return Loc.GetString("luam-admin-jobs-error-unavailable");

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{player.Name} changed the {job.ID} slots of {ToPrettyString(station.Value):station} by {ev.Amount} from the admin panel, it had {slots}");
        return null;
    }

    private void SendState(ICommonSession player, string? error)
    {
        var entries = new List<AdminStationJobEntry>();
        var query = EntityQueryEnumerator<StationJobsComponent>();
        while (query.MoveNext(out var station, out var stationJobs))
        {
            // bought ships keep their own crew slots, the panel is for the faction stations
            if (HasComp<ExtraShuttleInformationComponent>(station))
                continue;

            foreach (var (jobId, slots) in _jobs.GetJobs(station, stationJobs))
            {
                if (slots is not >= 0 || !_proto.TryIndex(jobId, out var job))
                    continue;

                if (AdminStationJobFactions.FromCompany(job.AssignedCompany.Id) is { } faction)
                    entries.Add(new AdminStationJobEntry(GetNetEntity(station), Name(station), faction, job.ID, slots.Value));
            }
        }

        RaiseNetworkEvent(new AdminStationJobsStateEvent(entries, error), player);
    }
}

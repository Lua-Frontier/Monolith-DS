using Content.Server.Administration.Logs;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Components;
using Content.Server.Research.Systems;
using Content.Shared._LuaM.Research;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server._LuaM.Research;

/// <summary>
/// Handles the "research everything" buttons of the R&amp;D console: unlocks a whole discipline with all of its
/// prerequisites, or every discipline, in one go if the server has the points for all of it.
/// </summary>
public sealed partial class ResearchUnlockAllSystem : EntitySystem
{
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ResearchConsoleComponent, ConsoleUnlockAllTechnologiesMessage>(OnUnlockAll);
        SubscribeLocalEvent<ResearchConsoleComponent, ConsoleUnlockWithPrerequisitesMessage>(OnUnlockWithPrerequisites);
    }

    private void OnUnlockAll(Entity<ResearchConsoleComponent> ent, ref ConsoleUnlockAllTechnologiesMessage args)
    {
        var discipline = args.Discipline;
        TryUnlockPlan(ent, args.Actor, discipline?.Id ?? "all disciplines",
            database => ResearchUnlockAll.Plan(_prototypes, database.UnlockedTechnologies.Select(t => t.Id).ToList(), database.SupportedDisciplines.Select(d => d.Id).ToList(), discipline));
    }

    private void OnUnlockWithPrerequisites(Entity<ResearchConsoleComponent> ent, ref ConsoleUnlockWithPrerequisitesMessage args)
    {
        if (!_prototypes.TryIndex(args.Technology, out var technology))
            return;

        TryUnlockPlan(ent, args.Actor, $"{technology.ID} with prerequisites",
            database => ResearchUnlockAll.PlanFor(_prototypes, database.UnlockedTechnologies.Select(t => t.Id).ToList(), database.SupportedDisciplines.Select(d => d.Id).ToList(), technology));
    }

    /// <summary>
    /// Unlocks every planned technology at once, if the console may and the server has the points for all of them.
    /// </summary>
    private void TryUnlockPlan(
        Entity<ResearchConsoleComponent> ent,
        EntityUid user,
        string what,
        Func<TechnologyDatabaseComponent, List<TechnologyPrototype>> makePlan)
    {
        if (!this.IsPowered(ent, EntityManager))
            return;

        if (TryComp<AccessReaderComponent>(ent, out var access) && !_accessReader.IsAllowed(user, ent, access))
        {
            _popup.PopupEntity(Loc.GetString("research-console-no-access-popup"), user);
            return;
        }

        if (!_research.TryGetClientServer(ent, out var serverUid, out var server)
            || !TryComp<TechnologyDatabaseComponent>(serverUid, out var database))
            return;

        var plan = makePlan(database);
        if (plan.Count == 0)
            return;

        var cost = ResearchUnlockAll.TotalCost(plan);
        if (cost > server.Points)
        {
            _popup.PopupEntity(Loc.GetString("research-console-unlock-all-not-enough", ("cost", cost), ("points", server.Points)), user);
            return;
        }

        foreach (var technology in plan)
        {
            _research.AddTechnology(serverUid.Value, technology, database);
        }

        _research.ModifyServerPoints(serverUid.Value, -cost, server);
        _research.UpdateTechnologyCards(serverUid.Value, database);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} unlocked {plan.Count} technologies ({what}) for {cost} points at {ToPrettyString(ent)}, for server {ToPrettyString(serverUid.Value)}.");

        // Syncing the console also refreshes its interface.
        _research.SyncClientWithServer(ent);
    }
}

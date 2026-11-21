using Content.Server._Mono.Cleanup;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Console;

namespace Content.Server._LuaM.Cleanup;

/// <summary>
/// Schedules removal of abandoned grids without waiting for the cleanup timer.
/// With "preview" only lists them.
/// </summary>
[AdminCommand(AdminFlags.Spawn)]
public sealed partial class CleanupGridsCommand : LocalizedEntityCommands
{
    private const string PreviewArg = "preview";
    private const int PreviewLimit = 50;

    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private GridCleanupSystem _gridCleanup = default!;

    public override string Command => "cleanupgrids";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1 || args.Length == 1 && args[0] != PreviewArg)
        {
            shell.WriteError(Loc.GetString("cmd-cleanupgrids-help"));
            return;
        }

        if (args.Length == 1)
        {
            var grids = _gridCleanup.GetAbandonedGrids();
            if (grids.Count == 0)
            {
                shell.WriteLine(Loc.GetString("cmd-cleanupgrids-none"));
                return;
            }

            shell.WriteLine(Loc.GetString("cmd-cleanupgrids-preview", ("count", grids.Count)));
            for (var i = 0; i < grids.Count && i < PreviewLimit; i++)
            {
                shell.WriteLine($"  {EntityManager.ToPrettyString(grids[i])}");
            }

            if (grids.Count > PreviewLimit)
                shell.WriteLine(Loc.GetString("cmd-cleanupgrids-preview-more", ("count", grids.Count - PreviewLimit)));

            return;
        }

        if (!_gridCleanup.TryScheduleGridCleanup())
        {
            shell.WriteLine(Loc.GetString("cmd-cleanupgrids-no-new-pass"));
            return;
        }

        _adminLogger.Add(LogType.Action, LogImpact.High, $"{shell.Player?.Name ?? "Server"} scheduled forced grid cleanup");
        shell.WriteLine(Loc.GetString("cmd-cleanupgrids-scheduled"));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(new[] { PreviewArg }, Loc.GetString("cmd-cleanupgrids-hint"));

        return CompletionResult.Empty;
    }
}

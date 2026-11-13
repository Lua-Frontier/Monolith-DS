using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Console;

namespace Content.Server._LuaM.Nebula;

/// <summary>
/// Сразу переносит погоду опасных туманностей на безвредные, не дожидаясь таймера правила.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed partial class NebulaShiftCommand : LocalizedEntityCommands
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private NebulaShiftRuleSystem _nebulaShift = default!;

    public override string Command => "nebulashift";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Loc.GetString("cmd-nebulashift-help"));
            return;
        }

        var moved = _nebulaShift.ForceShift();
        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{shell.Player?.Name ?? "Server"} shifted {moved} hazardous nebulae");
        shell.WriteLine(Loc.GetString("cmd-nebulashift-done", ("count", moved)));
    }
}

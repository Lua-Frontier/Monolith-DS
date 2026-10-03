using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Server.Console.Commands;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server._LuaM.Mapping;

[AdminCommand(AdminFlags.Mapping)]
public sealed partial class LoadGridCleanCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IResourceManager _resource = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;

    public string Command => "loadgridclean";
    public string Description => _loc.GetString("cmd-loadgridclean-desc");
    public string Help => _loc.GetString("cmd-loadgridclean-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2 || args.Length == 3 || args.Length > 6)
        {
            shell.WriteError(Help);
            return;
        }

        if (!int.TryParse(args[0], out var intMapId))
        {
            shell.WriteError(_loc.GetString("cmd-loadgridclean-bad-map", ("value", args[0])));
            return;
        }

        var mapId = new MapId(intMapId);
        if (mapId == MapId.Nullspace)
        {
            shell.WriteError(_loc.GetString("cmd-loadgridclean-nullspace"));
            return;
        }

        var path = new ResPath(args[1]);
        if (path.EnumerateSegments().Any(segment => segment == ".."))
        {
            shell.WriteError(_loc.GetString("cmd-loadgridclean-bad-path"));
            return;
        }

        var offset = Vector2.Zero;
        if (args.Length >= 4)
        {
            if (!float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                shell.WriteError(_loc.GetString("cmd-loadgridclean-bad-float"));
                return;
            }

            offset = new Vector2(x, y);
        }

        var rotation = Angle.Zero;
        if (args.Length >= 5)
        {
            if (!float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees))
            {
                shell.WriteError(_loc.GetString("cmd-loadgridclean-bad-float"));
                return;
            }

            rotation = Angle.FromDegrees(degrees);
        }

        var options = DeserializationOptions.Default;
        if (args.Length >= 6)
        {
            if (!bool.TryParse(args[5], out var storeUids))
            {
                shell.WriteError(_loc.GetString("cmd-loadgridclean-bad-bool", ("value", args[5])));
                return;
            }

            options.StoreYamlUids = storeUids;
        }

        var mapSystem = _entManager.System<SharedMapSystem>();
        if (!mapSystem.MapExists(mapId))
        {
            shell.WriteLine(_loc.GetString("cmd-loadgridclean-map-created", ("map", intMapId)));
            mapSystem.CreateMap(mapId, false);
        }

        var loader = _entManager.System<CleanGridLoaderSystem>();
        if (!loader.TryLoadGrid(mapId, path, options, offset, rotation, out var grid, out var report, out var error))
        {
            shell.WriteError(_loc.GetString("cmd-loadgridclean-failed", ("reason", error ?? "load")));
            return;
        }

        foreach (var (id, count) in report.Missing.OrderBy(pair => pair.Key))
        {
            shell.WriteLine(_loc.GetString("cmd-loadgridclean-missing-entry", ("id", id), ("count", count)));
        }

        foreach (var id in report.MissingTiles.OrderBy(tile => tile))
        {
            shell.WriteLine(_loc.GetString("cmd-loadgridclean-missing-tile",
                ("id", id), ("fallback", CleanGridLoaderSystem.FallbackTile)));
        }

        foreach (var (id, count) in report.MissingDecals.OrderBy(pair => pair.Key))
        {
            shell.WriteLine(_loc.GetString("cmd-loadgridclean-missing-decal", ("id", id), ("count", count)));
        }

        foreach (var (id, count) in report.MissingComponents.OrderBy(pair => pair.Key))
        {
            shell.WriteLine(_loc.GetString("cmd-loadgridclean-missing-component", ("id", id), ("count", count)));
        }

        shell.WriteLine(_loc.GetString("cmd-loadgridclean-success",
            ("grid", _entManager.GetNetEntity(grid.Value.Owner)),
            ("types", report.Missing.Count),
            ("removed", report.Removed),
            ("rescued", report.Rescued),
            ("tiles", report.MissingTiles.Count),
            ("decals", report.MissingDecals.Values.Sum()),
            ("components", report.MissingComponents.Values.Sum())));

        _adminLogger.Add(LogType.Action,
            LogImpact.High,
            $"{shell.Player?.Name ?? "server console"} loaded grid {path} onto map {intMapId} with loadgridclean: " +
            $"{_entManager.ToPrettyString(grid.Value.Owner)}, skipped prototypes: {string.Join(", ", report.Missing.Keys)}, " +
            $"replaced tiles: {string.Join(", ", report.MissingTiles)}, removed decals: {string.Join(", ", report.MissingDecals.Keys)}, " +
            $"removed components: {string.Join(", ", report.MissingComponents.Keys)}");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return LoadMap.GetCompletionResult(shell, args, _resource, _loc);
    }
}

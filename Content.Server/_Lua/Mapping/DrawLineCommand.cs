// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See LICENSE-AGPLv3.txt for details.
using Robust.Shared.Console;

namespace Content.Server._Lua.Mapping;

public sealed partial class DrawLineCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;

    public string Command => "drawline";
    public string Description => IoCManager.Resolve<ILocalizationManager>().GetString("cmd-drawline-desc"); // LuaM: ""Рисует крест квадратами до 1000 тайлов от вашей клетки (видно только вам)";"; > IoCManager.Resolve<ILocalizationManager>().GetString("cmd-drawline-desc")
    public string Help => "drawline";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var sys = _entManager.System<DrawLineSystem>();
        var player = shell.Player;
        if (player == null) return;
        sys.ToggleDrawForSession(player);
    }
}
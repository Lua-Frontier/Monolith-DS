// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See LICENSE-AGPLv3.txt for details.
using Robust.Shared.Console;
using Robust.Shared.IoC; // LuaM
using Robust.Shared.Localization; // LuaM

namespace Content.Client._Lua.Mapping;

public sealed class DrawLineClientCommand : IConsoleCommand
{
    public string Command => "drawline";
    public string Description => IoCManager.Resolve<ILocalizationManager>().GetString("cmd-drawline-desc"); // LuaM: "Линейка и категории шаттлов"; > IoCManager.Resolve<ILocalizationManager>().GetString("cmd-drawline-desc")
    public string Help => "drawline";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var sys = EntitySystem.Get<DrawLineSystem>();
        sys.SendDrawRequest();
    }
}
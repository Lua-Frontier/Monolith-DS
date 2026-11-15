using Content.Shared.Administration;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client._LuaM.Console;

[UsedImplicitly, AnyCommand]
public sealed partial class CopyErrorsCommand : LocalizedEntityCommands
{
    [Dependency] private IClipboardManager _clipboard = default!;
    [Dependency] private ConsoleErrorBufferSystem _errors = default!;

    public override string Command => "copyerrors";

    public override string Help => LocalizationManager.GetString($"cmd-{Command}-help", ("command", Command));

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var count = 1;
        if (args.Length > 0 && (!int.TryParse(args[0], out count) || count < 1))
        {
            shell.WriteError(LocalizationManager.GetString("cmd-copyerrors-invalid-count"));
            return;
        }

        var errors = _errors.GetLast(count);
        if (errors.Count == 0)
        {
            shell.WriteLine(LocalizationManager.GetString("cmd-copyerrors-empty"));
            return;
        }

        _clipboard.SetText(string.Join("\n\n", errors));
        shell.WriteLine(LocalizationManager.GetString("cmd-copyerrors-copied", ("count", errors.Count)));
    }
}

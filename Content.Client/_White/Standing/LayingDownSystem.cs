using Content.Shared._White.Standing;
using Content.Shared.CCVar; // LuaM
using Robust.Client.Player; // LuaM
using Robust.Shared.Configuration; // LuaM

namespace Content.Client._White.Standing;

public sealed partial class LayingDownSystem : SharedLayingDownSystem
{
    // LuaM-start
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override bool ShouldAutoGetUp(EntityUid uid, LayingDownComponent layingDown)
    {
        if (uid != _player.LocalEntity)
            return layingDown.AutoGetUp;

        return _cfg.GetCVar(CCVars.AutoGetUp);
    }
    // LuaM-end
}

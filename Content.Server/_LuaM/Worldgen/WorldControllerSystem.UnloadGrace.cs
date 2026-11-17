using Content.Server.Worldgen.Components;
using Content.Shared._LuaM.Performance;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;

namespace Content.Server.Worldgen.Systems;

public sealed partial class WorldControllerSystem
{
    [Dependency] private IConfigurationManager _perfCfg = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _unneededSince = new();
    private TimeSpan _unloadGrace;

    private void InitializeUnloadGrace()
    {
        _perfCfg.OnValueChanged(PerformanceCVars.ChunkUnloadGrace, s => _unloadGrace = TimeSpan.FromSeconds(s), true);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _unneededSince.Clear());
    }

    private bool ShouldUnload(EntityUid chunk, bool needed)
    {
        if (needed)
        {
            _unneededSince.Remove(chunk);
            return false;
        }

        var now = _gameTiming.CurTime;
        if (!_unneededSince.TryGetValue(chunk, out var since))
        {
            _unneededSince[chunk] = since = now;
        }

        if (now - since < _unloadGrace)
            return false;

        _unneededSince.Remove(chunk);
        return true;
    }
}

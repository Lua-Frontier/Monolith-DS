using System.Diagnostics.CodeAnalysis;
using Content.Shared.Shuttles.BUIStates;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    private static readonly TimeSpan DocksCacheLifetime = TimeSpan.FromSeconds(0.5);

    private Dictionary<NetEntity, List<DockingPortState>>? _cachedDocks;
    private TimeSpan _cachedDocksUntil;

    private bool TryGetCachedDocks([NotNullWhen(true)] out Dictionary<NetEntity, List<DockingPortState>>? docks)
    {
        docks = _cachedDocks;
        return docks != null && _timing.CurTime < _cachedDocksUntil;
    }

    private void CacheDocks(Dictionary<NetEntity, List<DockingPortState>> docks)
    {
        _cachedDocks = docks;
        _cachedDocksUntil = _timing.CurTime + DocksCacheLifetime;
    }

    public void ForgetCachedDocks()
    {
        _cachedDocks = null;
    }
}

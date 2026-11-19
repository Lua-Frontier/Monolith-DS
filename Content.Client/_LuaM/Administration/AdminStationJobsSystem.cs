using Content.Shared._LuaM.Administration;

namespace Content.Client._LuaM.Administration;

/// <summary>
/// Talks to the server for the job slots tab of the admin panel.
/// </summary>
public sealed class AdminStationJobsSystem : EntitySystem
{
    public event Action<AdminStationJobsStateEvent>? StateReceived;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<AdminStationJobsStateEvent>(OnState);
    }

    private void OnState(AdminStationJobsStateEvent ev)
    {
        StateReceived?.Invoke(ev);
    }

    public void RequestState()
    {
        RaiseNetworkEvent(new AdminStationJobsRequestEvent());
    }

    /// <summary>
    /// Positive amount opens slots, negative closes them.
    /// </summary>
    public void Change(NetEntity station, string job, int amount)
    {
        RaiseNetworkEvent(new AdminStationJobsChangeEvent(station, job, amount));
    }
}

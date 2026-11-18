using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Administration;

/// <summary>
/// The factions shown on the job slots tab of the admin panel, in display order. Their ids are company ids.
/// </summary>
public static class AdminStationJobFactions
{
    public static readonly string[] Order = ["TSF", "PDV", "TheViperGroup", "USSP", "MD", "Colonial", "MMC"];

    /// <summary>
    /// Gets the faction a job company belongs to, null if it isn't managed from the panel.
    /// </summary>
    public static string? FromCompany(string company)
    {
        if (company == "PDVCivilian")
            return "PDV";

        return Array.IndexOf(Order, company) >= 0 ? company : null;
    }
}

/// <summary>
/// Sent by the admin panel to get the job slots of the faction stations.
/// </summary>
[Serializable, NetSerializable]
public sealed class AdminStationJobsRequestEvent : EntityEventArgs;

/// <summary>
/// Sent by the admin panel to open (positive amount) or close (negative amount) job slots.
/// </summary>
[Serializable, NetSerializable]
public sealed class AdminStationJobsChangeEvent(NetEntity station, string job, int amount) : EntityEventArgs
{
    public readonly NetEntity Station = station;
    public readonly string Job = job;
    public readonly int Amount = amount;
}

/// <summary>
/// The job slots of the faction stations, sent to the admin who asked for them.
/// </summary>
[Serializable, NetSerializable]
public sealed class AdminStationJobsStateEvent(List<AdminStationJobEntry> entries, string? error) : EntityEventArgs
{
    public readonly List<AdminStationJobEntry> Entries = entries;

    /// <summary>
    /// Why the last change failed, already localized. Null if it went through or nothing was changed.
    /// </summary>
    public readonly string? Error = error;
}

[Serializable, NetSerializable]
public readonly record struct AdminStationJobEntry(NetEntity Station, string StationName, string Faction, string Job, int Slots);

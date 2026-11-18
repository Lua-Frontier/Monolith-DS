using Robust.Shared.Serialization;

namespace Content.Shared._NF.StationRecords;

[Serializable, NetSerializable]
public sealed class SetStationAdvertisementMsg : BoundUserInterfaceMessage
{
    public string Advertisement { get; }

    public SetStationAdvertisementMsg(string advertisement)
    {
        Advertisement = advertisement;
    }
}

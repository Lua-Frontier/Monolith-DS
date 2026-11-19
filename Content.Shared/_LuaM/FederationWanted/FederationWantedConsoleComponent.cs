using Content.Shared._EinsteinEngines.Language;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.FederationWanted;

/// <summary>
/// Shows everyone the Federation wants, and reports new names over the radio.
/// </summary>
[RegisterComponent]
public sealed partial class FederationWantedConsoleComponent : Component
{
    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel = "Nfsd";

    [DataField]
    public LocId Message = "federation-wanted-identified";

    [DataField]
    public ProtoId<LanguagePrototype> Language = "SolCommon";
}

[Serializable, NetSerializable]
public sealed class FederationWantedConsoleState(List<FederationWantedEntry> entries) : BoundUserInterfaceState
{
    public readonly List<FederationWantedEntry> Entries = entries;
}

[Serializable, NetSerializable]
public enum FederationWantedConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public readonly record struct FederationWantedEntry(string Name, int Reward);

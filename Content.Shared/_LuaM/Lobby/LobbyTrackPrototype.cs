using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._LuaM.Lobby;

[Prototype("lobbyTrack")]
public sealed partial class LobbyTrackPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ResPath Path;

    [DataField]
    public string? Title;

    [DataField]
    public string? Artist;

    [DataField]
    public bool Default;
}

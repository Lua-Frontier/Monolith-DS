using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._LuaM.Lobby;

[Prototype]
public sealed partial class AnimatedLobbyBackgroundPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ResPath Path;

    [DataField(required: true)]
    public int Frames;

    [DataField]
    public string Extension = "webp";

    [DataField]
    public float FrameTime = 0.1f;

    [DataField]
    public List<float> Delays = new();

    [DataField]
    public bool Default;
}

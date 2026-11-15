using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.GameTicking;

/// <summary>
/// Game rules that the game never starts on its own here.
/// They are left out of the addgamerule list and the command refuses them.
/// Code that starts them directly is not affected.
/// </summary>
[Prototype]
public sealed partial class HiddenGameRulesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<EntProtoId> Rules = new();
}

using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Traits;

/// <summary>
/// What withdrawal does to the body every time it rolls its effects.
/// </summary>
[DataDefinition]
public sealed partial class ChemicalWithdrawalEffects
{
    [DataField]
    public bool Jitter = true;

    [DataField]
    public float KnockdownChance = 0.3f;

    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(3);

    [DataField]
    public float DropChance = 0.3f;

    [DataField]
    public float EmoteChance = 0.35f;

    [DataField]
    public List<ProtoId<EmotePrototype>> Emotes = new() { "Scream", "Cough", "Crying", "Gasp", "Sneeze" };

    [DataField]
    public float PopupChance = 0.2f;

    [DataField]
    public List<LocId> Popups = new()
    {
        "chemical-dependency-withdrawal-1",
        "chemical-dependency-withdrawal-2",
        "chemical-dependency-withdrawal-3",
    };
}

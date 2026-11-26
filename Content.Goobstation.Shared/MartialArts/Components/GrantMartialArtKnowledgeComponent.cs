// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.NPC.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.MartialArts.Components;

/// <summary>
/// Teaches a martial art when used in hand. When added to a mob, the mob knows the art from the start.
/// </summary>
[RegisterComponent]
public sealed partial class GrantMartialArtKnowledgeComponent : Component
{
    [DataField(required: true)]
    public MartialArtsForms MartialArtsForm;

    [DataField]
    public LocId? LearnMessage;

    [DataField]
    public bool MultiUse;

    /// <summary>
    /// Spawned in place of the used up item.
    /// </summary>
    [DataField]
    public EntProtoId? SpawnedProto = "Ash";

    [DataField]
    public SoundSpecifier? SoundOnUse = new SoundPathSpecifier("/Audio/Effects/fire.ogg", AudioParams.Default.WithVolume(10));
}

/// <summary>
/// Teaches a martial art while the clothing is worn.
/// </summary>
[RegisterComponent]
public sealed partial class GrantMartialArtOnEquipComponent : Component
{
    [DataField(required: true)]
    public MartialArtsForms MartialArtsForm;

    [DataField]
    public LocId? LearnMessage;
}

/// <summary>
/// The Sleeping Carp scroll, it has to be studied several times with a delay.
/// </summary>
[RegisterComponent]
public sealed partial class GrantSleepingCarpComponent : Component
{
    [DataField]
    public int MaximumUses = 1;

    [DataField]
    public int CurrentUses;

    /// <summary>
    /// How many times the scroll has to be studied.
    /// </summary>
    [DataField]
    public int Stages = 3;

    [DataField]
    public ProtoId<NpcFactionPrototype> FactionToAdd = "Dragon";

    [DataField]
    public LocId LearnMessage = "carp-scroll-complete";
}

[RegisterComponent]
public sealed partial class SleepingCarpStudentComponent : Component
{
    [DataField]
    public int Stage;

    [DataField]
    public TimeSpan UseAgainTime = TimeSpan.Zero;

    [DataField]
    public TimeSpan MinUseDelay = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan MaxUseDelay = TimeSpan.FromSeconds(90);
}

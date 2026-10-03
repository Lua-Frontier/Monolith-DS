// SPDX-FileCopyrightText: 2026 LuaMonolith contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Actions;

public sealed partial class OnRetractableItemActionEvent : InstantActionEvent;
public sealed partial class TriggerActionEvent : InstantActionEvent;
public sealed partial class ObeliskActionEvent : InstantActionEvent;
public sealed partial class ObeliskActivateActionEvent : EntityTargetActionEvent;

[RegisterComponent]
public sealed partial class RetractableItemActionComponent : Component
{
    [DataField(required: true)]
    public EntProtoId SpawnedPrototype;

    [DataField]
    public SoundSpecifier? SummonSounds;

    [DataField]
    public SoundSpecifier? RetractSounds;

    [ViewVariables]
    public EntityUid? SpawnedEntity;
}

[RegisterComponent]
public sealed partial class TriggerOnActionComponent : Component
{
    [DataField]
    public string? KeyOut;
}
/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 * Ported from Wolfgate (_WF/FtlEffects), AGPLv3.
 */

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._LuaM.Shuttles.FtlEffects;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause, UnsavedComponent]
public sealed partial class FtlDepartureComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan Started;

    // -- Плановое время ухода; при входе в гиперпространство заменяется фактическим
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan Departure;

    [DataField, AutoNetworkedField]
    public bool Entered;

    [DataField, AutoNetworkedField]
    public bool Arriving;

    // -- Ведущий корабль сцепки; у самого ведущего null
    [DataField, AutoNetworkedField]
    public EntityUid? Lead;
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.TheCircle.CursedVant;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CursedVantComponent : Component
{
    [DataField, AutoNetworkedField]
    public float SpeedModifier = 0.5f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CircleDeaconComponent : Component;

using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.TheCircle.FrontierWar;

public sealed partial class CircleFrontierWarAcceptDreadnoughtEvent : InstantActionEvent;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CircleFrontierWarNavigationComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Visible = true;

    [DataField, AutoNetworkedField]
    public byte Side;

    [DataField, AutoNetworkedField]
    public bool ObjectivesVisible;

    [DataField, AutoNetworkedField]
    public int CrewReinforcements;

    [DataField, AutoNetworkedField]
    public int CircleReinforcements;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CircleFrontierWarNavigationTargetComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Label = string.Empty;

    // 0 = neutral, 1 = crew, 2 = Circle.
    [DataField, AutoNetworkedField]
    public byte Side;

    // 0 = capture point, 1 = side base, 2 = central obelisk.
    [DataField, AutoNetworkedField]
    public byte Kind;
}

public sealed partial class CircleFrontierWarToggleNavigationEvent : InstantActionEvent;

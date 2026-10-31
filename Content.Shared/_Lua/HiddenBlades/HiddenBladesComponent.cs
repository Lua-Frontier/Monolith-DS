using Robust.Shared.GameStates;

namespace Content.Shared._Lua.HiddenBlades;

/// <summary>
/// Gloves with blades that extend through <c>ItemToggle</c>: renames the item and shows a popup while the blades are out.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedHiddenBladesSystem))]
public sealed partial class HiddenBladesComponent : Component
{
    [DataField, AutoNetworkedField]
    public LocId ActivatedName = "hidden-blades-gloves-name-active";

    [DataField, AutoNetworkedField]
    public LocId ActivatedDescription = "hidden-blades-gloves-desc-active";

    [DataField, AutoNetworkedField]
    public LocId ActivatedPopUp = "hidden-blades-gloves-activated";

    [DataField, AutoNetworkedField]
    public LocId DeactivatedPopUp = "hidden-blades-gloves-deactivated";
}

using Content.Shared._LuaM.Delivery;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.Delivery;

[RegisterComponent, Access(typeof(DeliverySystem))]
public sealed partial class DeliveryCourierComponent : Component
{
    [ViewVariables]
    public int Delivered;

    [ViewVariables]
    public int Earned;
}

[RegisterComponent, Access(typeof(DeliverySystem))]
public sealed partial class DeliveryParcelComponent : Component
{
    [ViewVariables]
    public int OrderId;

    [DataField]
    public SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/Effects/packetrip.ogg");

    [DataField]
    public SoundSpecifier BreakSound = new SoundCollectionSpecifier("MetalBreak");

    [DataField]
    public EntProtoId BrokenSpawn = "SheetSteel1";
}

[RegisterComponent, Access(typeof(DeliverySystem))]
public sealed partial class DeliveryTelepadComponent : Component
{
    [DataField]
    public EntProtoId ParcelPrototype = "DeliveryParcel";

    [DataField]
    public SoundSpecifier ArriveSound = new SoundPathSpecifier("/Audio/Machines/phasein.ogg");
}

[RegisterComponent, Access(typeof(DeliverySystem))]
public sealed partial class DeliveryCartridgeComponent : Component
{
    [ViewVariables]
    public DeliveryUiMode? LastMode;
}

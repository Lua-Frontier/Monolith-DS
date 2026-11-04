using Content.Shared._LuaM.SprayPainter; // LuaM
using Content.Shared.Decals; // LuaM
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes; // LuaM

namespace Content.Shared.SprayPainter.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)] // LuaM: raise AfterAutoHandleStateEvent
public sealed partial class SprayPainterComponent : Component
{
    [DataField]
    public SoundSpecifier SpraySound = new SoundPathSpecifier("/Audio/Effects/spray2.ogg");

    [DataField]
    public TimeSpan AirlockSprayTime = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan PipeSprayTime = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Pipe color chosen to spray with.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? PickedColor;

    /// <summary>
    /// Pipe colors that can be selected.
    /// </summary>
    [DataField]
    public Dictionary<string, Color> ColorPalette = new();

    /// <summary>
    /// Airlock style index selected.
    /// After prototype reload this might not be the same style but it will never be out of bounds.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Index;

    // LuaM-start
    /// <summary>
    /// Whether clicking the floor paints decals, removes them, or does nothing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DecalPaintMode DecalMode = DecalPaintMode.Off;

    [DataField, AutoNetworkedField]
    public ProtoId<DecalPrototype> SelectedDecal = "Arrows";

    /// <summary>
    /// Color to paint the decal in, null keeps the decal's own color.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? SelectedDecalColor;

    [DataField, AutoNetworkedField]
    public int SelectedDecalAngle;

    [DataField, AutoNetworkedField]
    public bool SnapDecals = true;

    /// <summary>
    /// Charges of paint spent per airlock, pipe and decal.
    /// </summary>
    [DataField]
    public int AirlockChargeCost = 1;

    [DataField]
    public int PipeChargeCost = 1;

    [DataField]
    public int DecalChargeCost = 1;

    [DataField]
    public SoundSpecifier SoundSwitchDecalMode = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg", AudioParams.Default.WithVolume(1.5f));
    // LuaM-end
}

using Content.Shared.Storage;
using Content.Shared.Whitelist; // LuaM
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.SafetyDepositBox.Components;

/// <summary>
/// A physical box that stores items and can be deposited into a console for persistence.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SafetyDepositBoxComponent : Component
{
    /// <summary>
    /// Unique ID for this deposit box, assigned when purchased.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Guid? BoxId;

    /// <summary>
    /// The user ID of the owner of this box.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Guid? OwnerId;

    /// <summary>
    /// The character profile index (slot number) of the owner.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int? CharacterIndex;

    /// <summary>
    /// Display name of the owner.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string? OwnerName;

    /// <summary>
    /// I think you get the idea.
    /// </summary>
    [DataField(required:true), ViewVariables(VVAccess.ReadWrite)]
    public int Cost;

    // LuaM-start
    /// <summary>
    /// Exact item prototypes that can be stored even when the storage blacklist matches them.
    /// Prototypes inheriting from these are not exempt.
    /// </summary>
    [DataField]
    public HashSet<EntProtoId> BlacklistExceptions = new();

    /// <summary>
    /// Items matching this are refused for holding reagents, unless they are clothing that passes
    /// <see cref="ClothingReagentBlacklist"/>.
    /// </summary>
    [DataField]
    public EntityWhitelist? ReagentBlacklist;

    /// <summary>
    /// Clothing matching this is refused because reagents can be put into it or taken out of it.
    /// </summary>
    [DataField]
    public EntityWhitelist? ClothingReagentBlacklist;
    // LuaM-end
}

[Serializable, NetSerializable]
public enum SafetyDepositBoxVisuals : byte
{
    Locked,
}

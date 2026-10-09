using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.WarDeclaration;

/// <summary>
/// Sent by the communications console UI to declare war for the console's faction.
/// </summary>
[Serializable, NetSerializable]
public sealed class CommunicationsConsoleDeclareWarMessage : BoundUserInterfaceMessage
{
    public readonly string Reason;

    public CommunicationsConsoleDeclareWarMessage(string reason)
    {
        Reason = reason;
    }
}

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Shuttles;

/// <summary>
/// Hacking a locked shuttle console with a cryptographic sequencer.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ShuttleConsoleHackDoAfterEvent : SimpleDoAfterEvent;

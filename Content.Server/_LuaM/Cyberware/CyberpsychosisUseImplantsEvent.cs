namespace Content.Server._LuaM.Cyberware;

/// <summary>
/// Raised on a creature in cyberpsychosis every so often. Implant systems turn their implant on if it is off
/// and ready: the psycho only turns things on, turning them off is left to the player.
/// </summary>
[ByRefEvent]
public record struct CyberpsychosisUseImplantsEvent;

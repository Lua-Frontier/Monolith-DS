using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.RoundIntro;

/// <summary>
/// Sent right before <see cref="Content.Shared.GameTicking.TickerJoinGameEvent"/> when the joining job gets an intro.
/// </summary>
[Serializable, NetSerializable]
public sealed class RoundIntroExpectEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class RoundIntroEvent(NetEntity entity, string name, ProtoId<JobPrototype> job, string? location) : EntityEventArgs
{
    public NetEntity Entity = entity;
    public string Name = name;
    public ProtoId<JobPrototype> Job = job;
    public string? Location = location;
}

[Serializable, NetSerializable]
public sealed class RoundIntroCoworkersEvent(List<RoundIntroCoworker> coworkers) : EntityEventArgs
{
    public List<RoundIntroCoworker> Coworkers = coworkers;
}

[Serializable, NetSerializable]
public readonly record struct RoundIntroCoworker(NetEntity Entity, string Name, ProtoId<JobPrototype> Job);

[ByRefEvent]
public record struct ClientJoinGameAttemptEvent(bool Handled = false);

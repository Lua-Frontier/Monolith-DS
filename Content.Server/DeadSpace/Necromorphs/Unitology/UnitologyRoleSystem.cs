// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.DeadSpace.Necromorphs.Roles;
using Content.Shared.DeadSpace.Necromorphs.Unitology.Components;
using Content.Shared.Mind;
using Content.Shared.Radio;
using Content.Server.Radio.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Necromorphs.Unitology;

/// <summary>
/// Grants and removes unitology roles without requiring the Unitology game rule.
/// </summary>
public sealed class UnitologyRoleSystem : EntitySystem
{
    public static readonly EntProtoId HeadMindRole = "MindRoleHeadUnitology";
    public static readonly EntProtoId RegularMindRole = "MindRoleUnitology";
    public static readonly EntProtoId EnslavedMindRole = "MindRoleEnslavedUnitology";

    private static readonly ProtoId<RadioChannelPrototype> UnitologyChannel = "Unitolog";
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RoleSystem _roles = default!;

    public bool GrantHead(EntityUid target)
    {
        EnsureUnitologyCommunication(target);
        EnsureComp<UnitologyComponent>(target);
        EnsureComp<UnitologyHeadComponent>(target);
        return SetMindRole(target, HeadMindRole);
    }

    public bool GrantRegular(EntityUid target)
    {
        EnsureUnitologyCommunication(target);
        EnsureComp<UnitologyComponent>(target);
        return SetMindRole(target, RegularMindRole);
    }

    public bool GrantEnslaved(EntityUid target)
    {
        EnsureUnitologyCommunication(target);
        EnsureComp<UnitologyComponent>(target);
        EnsureComp<UnitologyEnslavedComponent>(target);
        return SetMindRole(target, EnslavedMindRole);
    }

    public void RemoveUnitology(EntityUid target)
    {
        RemComp<UnitologyComponent>(target);
        RemoveUnitologyCommunication(target);

        if (!_mind.TryGetMind(target, out var mindId, out _))
            return;

        while (_roles.MindRemoveRole<UnitologyRoleComponent>(mindId))
        {
        }
    }

    private bool SetMindRole(EntityUid target, EntProtoId role)
    {
        if (!_mind.TryGetMind(target, out var mindId, out var mind))
            return false;

        while (_roles.MindRemoveRole<UnitologyRoleComponent>(mindId))
        {
        }

        _roles.MindAddRole(mindId, role, mind);
        return true;
    }

    private void EnsureUnitologyCommunication(EntityUid target)
    {
        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(target);
        if (transmitter.Channels.Add(UnitologyChannel))
            Dirty(target, transmitter);

        var active = EnsureComp<ActiveRadioComponent>(target);
        if (active.Channels.Add(UnitologyChannel))
            Dirty(target, active);

        EnsureComp<IntrinsicRadioReceiverComponent>(target);
    }

    private void RemoveUnitologyCommunication(EntityUid target)
    {
        if (TryComp<IntrinsicRadioTransmitterComponent>(target, out var transmitter))
        {
            transmitter.Channels.Remove(UnitologyChannel);
            if (transmitter.Channels.Count == 0)
                RemCompDeferred<IntrinsicRadioTransmitterComponent>(target);
            else
                Dirty(target, transmitter);
        }

        if (TryComp<ActiveRadioComponent>(target, out var active))
        {
            active.Channels.Remove(UnitologyChannel);
            if (active.Channels.Count == 0)
                RemCompDeferred<ActiveRadioComponent>(target);
            else
                Dirty(target, active);
        }

        RemCompDeferred<IntrinsicRadioReceiverComponent>(target);
    }
}

using Content.Shared._WF.SafetyDepositBox.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;

namespace Content.Shared._LuaM.SafetyDepositBox;

/// <summary>
/// Decides which items a safety deposit box accepts.
/// </summary>
public sealed partial class SafetyDepositRulesSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public bool CanStore(SafetyDepositBoxComponent box, EntityWhitelist? blacklist, EntityUid item)
    {
        if (IsException(box, item))
            return true;

        if (_whitelist.IsBlacklistPass(blacklist, item))
            return false;

        if (!_whitelist.IsBlacklistPass(box.ReagentBlacklist, item))
            return true;

        // Clothing is edible and holds reagents because of that, which is fine while nothing can be put into it.
        // Boots and the like have item slots, so what they carry has to pass the blacklist as well.
        return HasComp<ClothingComponent>(item)
               && !_whitelist.IsBlacklistPass(box.ClothingReagentBlacklist, item)
               && !HoldsBlacklisted(box, blacklist, item);
    }

    private bool IsException(SafetyDepositBoxComponent box, EntityUid item)
    {
        return MetaData(item).EntityPrototype?.ID is { } id && box.BlacklistExceptions.Contains(id);
    }

    private bool HoldsBlacklisted(SafetyDepositBoxComponent box, EntityWhitelist? blacklist, EntityUid item)
    {
        if (!TryComp<ContainerManagerComponent>(item, out var manager))
            return false;

        foreach (var container in _container.GetAllContainers(item, manager))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (IsException(box, contained))
                    continue;

                if (_whitelist.IsBlacklistPass(blacklist, contained) || HoldsBlacklisted(box, blacklist, contained))
                    return true;
            }
        }

        return false;
    }
}

using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.SafetyDepositBox;

/// <summary>
/// Tells on examine that an item can not be put into a safety deposit box.
/// The blacklist is read from the box prototype, so the examine text always matches the real storage rules.
/// </summary>
public sealed partial class SafetyDepositBlacklistExamineSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    // all safety deposit boxes share the storage blacklist of their abstract parent
    private static readonly EntProtoId BoxPrototype = "SafetyDepositBoxSmall";

    private EntityWhitelist? _blacklist;
    private bool _blacklistLoaded;

    public override void Initialize()
    {
        base.Initialize();

        // ItemComponent examine is already taken by the item system
        SubscribeLocalEvent<MetaDataComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            _blacklistLoaded = false;
    }

    private void OnExamined(Entity<MetaDataComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<ItemComponent>(ent))
            return;

        if (!_whitelist.IsBlacklistPass(GetBlacklist(), ent))
            return;

        args.PushMarkup(Loc.GetString("safety-deposit-blacklisted-examine"), -1);
    }

    private EntityWhitelist? GetBlacklist()
    {
        if (_blacklistLoaded)
            return _blacklist;

        _blacklistLoaded = true;
        _blacklist = null;

        if (_prototype.TryIndex(BoxPrototype, out var proto) &&
            proto.TryComp<StorageComponent>(out var storage, Factory))
        {
            _blacklist = storage.Blacklist;
        }

        return _blacklist;
    }
}

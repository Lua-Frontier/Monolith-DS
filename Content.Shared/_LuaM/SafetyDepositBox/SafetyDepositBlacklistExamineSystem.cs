using Content.Shared._WF.SafetyDepositBox.Components;
using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.SafetyDepositBox;

/// <summary>
/// Tells on examine that an item can not be put into a safety deposit box.
/// The rules are read from the box prototype, so the examine text always matches the real storage rules.
/// </summary>
public sealed partial class SafetyDepositBlacklistExamineSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SafetyDepositRulesSystem _rules = default!;

    // all safety deposit boxes share the storage rules of their abstract parent
    private static readonly EntProtoId BoxPrototype = "SafetyDepositBoxSmall";

    private SafetyDepositBoxComponent? _box;
    private EntityWhitelist? _blacklist;
    private bool _rulesLoaded;

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
            _rulesLoaded = false;
    }

    private void OnExamined(Entity<MetaDataComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<ItemComponent>(ent))
            return;

        LoadRules();

        if (_box == null || _rules.CanStore(_box, _blacklist, ent))
            return;

        args.PushMarkup(Loc.GetString("safety-deposit-blacklisted-examine"), -1);
    }

    private void LoadRules()
    {
        if (_rulesLoaded)
            return;

        _rulesLoaded = true;
        _box = null;
        _blacklist = null;

        if (!_prototype.TryIndex(BoxPrototype, out var proto))
            return;

        if (proto.TryComp<SafetyDepositBoxComponent>(out var box, Factory))
            _box = box;

        if (proto.TryComp<StorageComponent>(out var storage, Factory))
            _blacklist = storage.Blacklist;
    }
}

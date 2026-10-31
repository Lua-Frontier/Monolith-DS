using Content.Goobstation.Common.Weapons.Multishot;
using Content.Shared._LuaM.Weapons;
using Content.Shared.Hands;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._LuaM.Cowboy;

public sealed partial class CowboySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    private enum GunClass : byte
    {
        Other,
        Pistol,
        SawnOff,
        Forbidden,
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CowboyComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<CowboyComponent, DidEquipHandEvent>(OnEquipped);
        SubscribeLocalEvent<CowboyComponent, DidUnequipHandEvent>(OnUnequipped);
        SubscribeLocalEvent<CowboyComponent, GunExaminedByEvent>(OnGunExamined);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<EntityPrototype>())
            return;

        var query = EntityQueryEnumerator<CowboyComponent>();
        while (query.MoveNext(out var cowboy))
        {
            cowboy.GunClassCache.Clear();
        }
    }

    /// <summary>
    /// True when the user is a cowboy and the gun is a pistol or a forbidden gun for them.
    /// The usual dual wield examine line is wrong for both, the cowboy trait writes its own.
    /// </summary>
    public bool HidesMultishotExamine(EntityUid user, EntityUid gun)
    {
        return TryComp<CowboyComponent>(user, out var cowboy) &&
               Classify(gun, cowboy) is GunClass.Pistol or GunClass.Forbidden;
    }

    /// <summary>
    /// True when the gun is a pistol held by a cowboy, the dual wield penalties are skipped for it.
    /// </summary>
    public bool IgnoresMultishotDebuffs(EntityUid gun)
    {
        if (!_container.TryGetContainingContainer(gun, out var container) ||
            !TryComp<CowboyComponent>(container.Owner, out var cowboy))
            return false;

        return Classify(gun, cowboy) == GunClass.Pistol;
    }

    private void OnGunExamined(Entity<CowboyComponent> ent, ref GunExaminedByEvent args)
    {
        var text = Classify(args.Gun, ent.Comp) switch
        {
            GunClass.Forbidden => "cowboy-examine-forbidden",
            GunClass.Pistol => "cowboy-examine-pistol",
            // a held sawn-off already has the dual wield component and tells its miss chance itself
            GunClass.SawnOff when !HasComp<MultishotComponent>(args.Gun) => "cowboy-examine-sawn-off",
            _ => null,
        };

        if (text != null)
            args.Examine.PushMarkup(Loc.GetString(text), -1);
    }

    private void OnShotAttempted(Entity<CowboyComponent> ent, ref ShotAttemptedEvent args)
    {
        if (Classify(args.Used, ent.Comp) != GunClass.Forbidden)
            return;

        args.Cancel();

        if (_timing.CurTime < ent.Comp.NextPopup)
            return;

        ent.Comp.NextPopup = _timing.CurTime + ent.Comp.PopupCooldown;
        _popup.PopupClient(Loc.GetString("cowboy-cannot-fire"), ent, ent);
    }

    // Sawn-offs and pistols that can't be dual wielded (like the Echis) get it while a cowboy holds them.
    // The user event is raised before the item one, so the multishot system already sees the new component.
    private void OnEquipped(EntityUid uid, CowboyComponent comp, DidEquipHandEvent args)
    {
        if (_net.IsClient ||
            !HasComp<GunComponent>(args.Equipped) ||
            HasComp<MultishotComponent>(args.Equipped) ||
            Classify(args.Equipped, comp) is not (GunClass.SawnOff or GunClass.Pistol))
            return;

        AddComp<MultishotComponent>(args.Equipped);
        AddComp<CowboyGrantedMultishotComponent>(args.Equipped);
    }

    private void OnUnequipped(EntityUid uid, CowboyComponent comp, DidUnequipHandEvent args)
    {
        if (_net.IsClient || !HasComp<CowboyGrantedMultishotComponent>(args.Unequipped))
            return;

        RemCompDeferred<MultishotComponent>(args.Unequipped);
        RemCompDeferred<CowboyGrantedMultishotComponent>(args.Unequipped);
    }

    private GunClass Classify(EntityUid gun, CowboyComponent cowboy)
    {
        if (MetaData(gun).EntityPrototype is not { } proto)
            return GunClass.Other;

        if (cowboy.GunClassCache.TryGetValue(proto.ID, out var cached))
            return (GunClass) cached;

        var result = ClassifyPrototype(proto.ID, cowboy);
        cowboy.GunClassCache[proto.ID] = (byte) result;
        return result;
    }

    private GunClass ClassifyPrototype(string protoId, CowboyComponent cowboy)
    {
        var pistol = false;
        var forbidden = false;
        foreach (var (id, _) in _prototype.EnumerateAllParents<EntityPrototype>(protoId, true))
        {
            // sawn-offs inherit from shotguns, so they win over the forbidden list
            if (cowboy.SawnOffBases.Contains(id))
                return GunClass.SawnOff;

            pistol |= cowboy.PistolBases.Contains(id);
            forbidden |= cowboy.ForbiddenBases.Contains(id);
        }

        if (pistol)
            return GunClass.Pistol;

        return forbidden ? GunClass.Forbidden : GunClass.Other;
    }
}

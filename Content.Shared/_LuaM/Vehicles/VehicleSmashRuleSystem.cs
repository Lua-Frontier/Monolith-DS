/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Vehicles;

public sealed partial class VehicleSmashRuleSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly string[] NeverParents = { "BaseWall", "BaseWallIndestructible", "BaseStructureIndestructible" };
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    // -- Destructible есть только у сервера: клиент, проверяя его, упирался бы во всё, что сервер сносит
    private const string ServerOnlyComponent = "Destructible";

    private List<VehicleSmashRulePrototype>? _rules;
    private readonly Dictionary<string, VehicleSmashRulePrototype?> _resolved = new();
    private readonly HashSet<string> _parents = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<VehicleSmashRulePrototype>() && !args.WasModified<EntityPrototype>())
            return;

        _rules = null;
        _resolved.Clear();
    }

    public bool TryGetRule(EntityUid uid, [NotNullWhen(true)] out VehicleSmashRulePrototype? rule)
    {
        rule = null;
        if (MetaData(uid).EntityPrototype is not { } proto)
            return false;

        if (!_resolved.TryGetValue(proto.ID, out rule))
        {
            rule = Resolve(proto);
            _resolved[proto.ID] = rule;
        }

        return rule != null;
    }

    private VehicleSmashRulePrototype? Resolve(EntityPrototype proto)
    {
        if (!proto.Components.ContainsKey("Damageable") ||
            !_net.IsClient && !proto.Components.ContainsKey(ServerOnlyComponent))
        {
            return null;
        }

        var tags = GetTags(proto);
        if (tags != null && tags.Contains(WallTag))
            return null;

        _parents.Clear();
        foreach (var (id, _) in _proto.EnumerateAllParents<EntityPrototype>(proto.ID, includeSelf: true))
        {
            _parents.Add(id);
        }

        foreach (var never in NeverParents)
        {
            if (_parents.Contains(never))
                return null;
        }

        foreach (var rule in GetRules())
        {
            if (Matches(rule, proto, tags))
                return rule;
        }

        return null;
    }

    private List<VehicleSmashRulePrototype> GetRules()
    {
        if (_rules != null)
            return _rules;

        _rules = new List<VehicleSmashRulePrototype>(_proto.EnumeratePrototypes<VehicleSmashRulePrototype>());
        _rules.Sort(static (a, b) =>
        {
            var byPriority = b.Priority.CompareTo(a.Priority);
            return byPriority != 0 ? byPriority : string.CompareOrdinal(a.ID, b.ID);
        });

        return _rules;
    }

    private bool Matches(VehicleSmashRulePrototype rule, EntityPrototype proto, HashSet<ProtoId<TagPrototype>>? tags)
    {
        if (rule.Parents.Count == 0 && rule.Whitelist == null)
            return false;

        if (rule.Parents.Count > 0 && !HasAnyParent(rule.Parents))
            return false;

        if (rule.Whitelist != null && !Passes(rule.Whitelist, proto, tags))
            return false;

        if (HasAnyParent(rule.ExcludeParents))
            return false;

        return rule.Blacklist == null || !Passes(rule.Blacklist, proto, tags);
    }

    private bool HasAnyParent(List<EntProtoId> parents)
    {
        foreach (var parent in parents)
        {
            if (_parents.Contains(parent.Id))
                return true;
        }

        return false;
    }

    private static bool Passes(EntityWhitelist list, EntityPrototype proto, HashSet<ProtoId<TagPrototype>>? tags)
    {
        if (list.Components != null)
        {
            foreach (var name in list.Components)
            {
                var has = proto.Components.ContainsKey(name);
                if (has && !list.RequireAll)
                    return true;

                if (!has && list.RequireAll)
                    return false;
            }
        }

        if (list.Tags != null)
        {
            foreach (var tag in list.Tags)
            {
                var has = tags != null && tags.Contains(tag);
                if (has && !list.RequireAll)
                    return true;

                if (!has && list.RequireAll)
                    return false;
            }
        }

        return list.RequireAll;
    }

    private static HashSet<ProtoId<TagPrototype>>? GetTags(EntityPrototype proto)
    {
        return proto.Components.TryGetValue("Tag", out var entry) && entry.Component is TagComponent tag ? tag.Tags : null;
    }
}

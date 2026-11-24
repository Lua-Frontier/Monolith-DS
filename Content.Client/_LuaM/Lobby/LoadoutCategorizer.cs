using System.Linq;
using System.Text.RegularExpressions;
using Content.Shared._LuaM.Loadouts;
using Content.Shared.Clothing;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Lobby;

public sealed class LoadoutCategorizer
{
    public const string OtherCategory = "Other";
    public const string StorageSlot = "storage";

    private static readonly Regex WordRegex = new(@"[A-Z]+(?![a-z])|[A-Z][a-z]+|[a-z]+|\d+", RegexOptions.Compiled);

    private readonly IPrototypeManager _proto;
    private readonly LoadoutSystem _loadout;
    private readonly List<(LoadoutCategoryPrototype Proto, Term[] Match, Term[] Exclude, Term[] Require)> _categories;

    public LoadoutCategorizer(IPrototypeManager proto, LoadoutSystem loadout)
    {
        _proto = proto;
        _loadout = loadout;
        _categories = proto.EnumeratePrototypes<LoadoutCategoryPrototype>()
            .OrderBy(c => c.Order)
            .Select(c => (c, Parse(c.Match), Parse(c.Exclude), Parse(c.Require)))
            .ToList();
    }

    public static string GetSlot(LoadoutPrototype loadout, IPrototypeManager proto)
    {
        IEquipmentLoadout gear = proto.TryIndex(loadout.StartingGear, out var starting) ? starting : loadout;
        return gear.Equipment.Count > 0 ? gear.Equipment.Keys.First() : StorageSlot;
    }

    public LoadoutCategoryPrototype? Categorize(LoadoutPrototype loadout)
    {
        var slot = GetSlot(loadout, _proto);
        var ent = loadout.PreviewEntity ?? loadout.DummyEntity ?? _loadout.GetFirstOrNull(loadout);

        if (ent == null)
            return Find(slot, StripPrefix(loadout.ID));

        var found = Find(slot, StripPrefix(ent.Value.Id));
        if (found != null || !_proto.TryIndex(ent.Value, out var entProto))
            return found;

        var parents = entProto.Parents;
        while (parents is { Length: > 0 })
        {
            found = Find(slot, parents[0]);
            if (found != null || !_proto.TryIndex<EntityPrototype>(parents[0], out var parent))
                return found;
            parents = parent.Parents;
        }

        return null;
    }

    private LoadoutCategoryPrototype? Find(string slot, string id)
    {
        var words = Words(id);
        var lower = id.ToLowerInvariant();

        foreach (var (proto, match, exclude, require) in _categories)
        {
            if (proto.Slots.Count > 0 && !proto.Slots.Contains(slot))
                continue;

            if (require.Length > 0 && !require.Any(t => t.Matches(words, lower)))
                continue;

            if (match.Any(t => t.Matches(words, lower)) && !exclude.Any(t => t.Matches(words, lower)))
                return proto;
        }

        return null;
    }

    private static string StripPrefix(string id)
    {
        var index = id.IndexOf("Clothing", StringComparison.Ordinal);
        return index > 0 ? id[index..] : id;
    }

    private static string[] Words(string id)
    {
        return WordRegex.Matches(id).Select(m => m.Value.ToLowerInvariant()).ToArray();
    }

    private static Term[] Parse(List<string> terms)
    {
        return terms.Select(t => t.StartsWith('~')
                ? new Term(null, t[1..].ToLowerInvariant())
                : new Term(Words(t), null))
            .ToArray();
    }

    private readonly record struct Term(string[]? Sequence, string? Substring)
    {
        public bool Matches(string[] words, string lower)
        {
            if (Substring != null)
                return lower.Contains(Substring, StringComparison.Ordinal);

            if (Sequence == null || Sequence.Length == 0)
                return false;

            for (var i = 0; i <= words.Length - Sequence.Length; i++)
            {
                var ok = true;
                for (var j = 0; j < Sequence.Length && ok; j++)
                    ok = words[i + j] == Sequence[j];

                if (ok)
                    return true;
            }

            return false;
        }
    }
}

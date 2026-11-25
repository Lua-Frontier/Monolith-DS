using System.Linq;
using Content.Client._LuaM.Lobby.Traits;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Traits;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private TraitsEditor? _traitsEditor;

    private void InitTraitsEditor()
    {
        var scroll = TraitsList.Parent!;
        scroll.Visible = false;

        _traitsEditor = new TraitsEditor();
        scroll.Parent!.AddChild(_traitsEditor);

        _traitsEditor.OnTraitToggled += ToggleTrait;
        _traitsEditor.OnClearAll += () =>
        {
            if (Profile == null)
                return;

            Profile = Profile.WithoutAllTraitPreferences();
            SetDirty();
            RefreshTraitsEditor();
        };
    }

    private void RefreshTraitsEditor()
    {
        if (_traitsEditor == null)
            return;

        var selected = Profile?.TraitPreferences ?? new HashSet<ProtoId<TraitPrototype>>();
        var species = Profile?.Species;
        var groups = new Dictionary<string, List<TraitPrototype>>();

        foreach (var trait in _prototypeManager.EnumeratePrototypes<TraitPrototype>())
        {
            if (!selected.Contains(trait.ID) && species is { } sp
                && (trait.SpeciesBlacklist.Contains(sp) || trait.SpeciesWhitelist.Count > 0 && !trait.SpeciesWhitelist.Contains(sp)))
                continue;

            var categoryId = trait.Category is { } cat && _prototypeManager.HasIndex(cat) ? cat.Id : TraitCategoryPrototype.Default;
            groups.GetOrNew(categoryId).Add(trait);
        }

        var data = new List<TraitCategoryView>();
        foreach (var (categoryId, traits) in groups)
        {
            TraitCategoryPrototype? category = null;
            int? max = null;
            string name;

            if (categoryId != TraitCategoryPrototype.Default)
            {
                category = _prototypeManager.Index<TraitCategoryPrototype>(categoryId);
                name = Loc.GetString(category.Name);
                if (category.MaxTraitPoints >= 0)
                    max = Profile?.GetMaxTraitPoints(category, _prototypeManager) ?? category.MaxTraitPoints;
            }
            else
            {
                name = Loc.GetString("humanoid-profile-editor-traits-default-category");
            }

            var spent = traits.Where(t => selected.Contains(t.ID)).Sum(t => t.Cost);
            var views = traits
                .OrderBy(t => t.Cost)
                .ThenBy(t => Loc.GetString(t.Name), StringComparer.CurrentCulture)
                .Select(t => GetTraitView(t, selected, spent, max))
                .ToList();

            data.Add(new TraitCategoryView(categoryId, name, spent, max, views));
        }

        data.Sort((a, b) =>
        {
            var ia = CategoryRank(a.Id);
            var ib = CategoryRank(b.Id);
            return ia != ib ? ia.CompareTo(ib) : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
        });
        _traitsEditor.SetData(data);
    }

    private static readonly string[] CategoryOrder = { "Physical", "LanguageTraits", "SpeechTraits", "Disabilities", "Quirks" };

    private static int CategoryRank(string id)
    {
        var index = Array.IndexOf(CategoryOrder, id);
        return index < 0 ? CategoryOrder.Length : index;
    }

    private TraitView GetTraitView(TraitPrototype trait, IReadOnlySet<ProtoId<TraitPrototype>> selected, int spent, int? max)
    {
        if (selected.Contains(trait.ID))
            return new TraitView(trait, TraitState.Selected, null);

        var exclusive = new List<string>();
        foreach (var other in selected)
        {
            if (!_prototypeManager.TryIndex(other, out var otherProto))
                continue;

            if (otherProto.MutuallyExclusiveTraits.Contains(trait.ID) || trait.MutuallyExclusiveTraits.Contains(other))
                exclusive.Add(Loc.GetString(otherProto.Name));
        }

        if (exclusive.Count > 0)
            return new TraitView(trait, TraitState.Blocked, Loc.GetString("traits-editor-exclusive", ("traits", string.Join(", ", exclusive))));

        if (max != null && trait.Cost > 0 && spent + trait.Cost > max)
            return new TraitView(trait, TraitState.Unaffordable, Loc.GetString("traits-editor-no-points"));

        return new TraitView(trait, TraitState.Available, null);
    }

    private void ToggleTrait(ProtoId<TraitPrototype> traitId)
    {
        if (Profile == null || !_prototypeManager.TryIndex(traitId, out var trait))
            return;

        if (!Profile.TraitPreferences.Contains(traitId))
        {
            Profile = Profile.WithTraitPreference(traitId, _prototypeManager);
        }
        else
        {
            var without = Profile.WithoutTraitPreference(traitId, _prototypeManager);
            if (without.GetValidTraits(without.TraitPreferences, _prototypeManager).Count < without.TraitPreferences.Count)
                return;

            Profile = without;
        }

        SetDirty();
        RefreshTraitsEditor();
    }
}

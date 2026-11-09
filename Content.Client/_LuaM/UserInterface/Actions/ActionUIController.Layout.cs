using System.Linq;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Shared._LuaM.CCVar;
using Content.Shared.Actions;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using static Content.Client.Actions.ActionsSystem;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.UserInterface.Systems.Actions;

public sealed partial class ActionUIController
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceManager _res = default!;

    private const int PresetCount = 10;
    private const int HiddenSlot = -1;
    private const string LayoutDirectory = "/luam/actions_layout";

    private static int ButtonsPerPreset => ActionButtonContainer.SlotsPerPreset;
    private static int TotalSlots => PresetCount * ButtonsPerPreset;

    private readonly Dictionary<int, List<string>> _savedLayout = new();
    private readonly HashSet<string> _hiddenActions = new();
    private bool _layoutLoaded;
    private string? _currentProfileKey;
    private int _currentPresetIndex;
    private bool _advancedHotbar;

    private int CurrentPresetStart => _advancedHotbar ? _currentPresetIndex * ButtonsPerPreset : 0;

    private void InitializeLayout()
    {
        _currentPresetIndex = Math.Clamp(_cfg.GetCVar(LuaMCVars.ActionsActivePreset), 0, PresetCount - 1);
        _cfg.OnValueChanged(LuaMCVars.AdvancedHotbar, OnAdvancedHotbarChanged, true);
    }

    private void OnAdvancedHotbarChanged(bool advanced)
    {
        if (_advancedHotbar == advanced)
            return;

        _advancedHotbar = advanced;
        ApplyHotbarMode();

        if (_actionsSystem == null || _playerManager.LocalEntity == null)
            return;

        StopTargeting();

        if (advanced)
        {
            _currentProfileKey = GetMobProfileKey();
            LoadLayout();
            RebuildLayout();
            UpdatePresetLabel();
        }
        else
        {
            SaveLayout();
            _layoutLoaded = false;
            LoadDefaultActions();
        }

        RefreshActionBar();
    }

    private void ApplyHotbarMode()
    {
        if (ActionsBar is not { } bar)
            return;

        bar.PageButtons.Visible = _advancedHotbar;
        bar.ActionsContainer.SetAdvanced(_advancedHotbar);
    }

    private void RefreshActionBar()
    {
        if (_advancedHotbar)
        {
            RebuildLayout();
            UpdateActionBar();
            return;
        }

        if (_actionsSystem != null)
            _container?.SetActionData(_actionsSystem, _actions.ToArray());
    }

    private void CommitActionBar()
    {
        if (_advancedHotbar)
            CommitLayout();
        else if (_actionsSystem != null)
            _container?.SetActionData(_actionsSystem, _actions.ToArray());
    }

    private void ChangePreset(int index)
    {
        _currentPresetIndex = (index % PresetCount + PresetCount) % PresetCount;
        _cfg.SetCVar(LuaMCVars.ActionsActivePreset, _currentPresetIndex);
        _cfg.SaveToFile();

        UpdatePresetLabel();
        UpdateActionBar();
    }

    private void OnLeftArrowPressed(ButtonEventArgs args)
    {
        ChangePreset(_currentPresetIndex - 1);
    }

    private void OnRightArrowPressed(ButtonEventArgs args)
    {
        ChangePreset(_currentPresetIndex + 1);
    }

    private void UpdatePresetLabel()
    {
        if (ActionsBar != null)
            ActionsBar.PageButtons.Label.Text = $"{_currentPresetIndex + 1}";
    }

    private EntityUid?[] GetCurrentPresetActions()
    {
        var preset = new EntityUid?[ButtonsPerPreset];
        for (var i = 0; i < ButtonsPerPreset; i++)
        {
            if (_actions.TryGetValue(CurrentPresetStart + i, out var action))
                preset[i] = action;
        }

        return preset;
    }

    private void UpdateActionBar()
    {
        if (_actionsSystem != null)
            _container?.SetActionData(_actionsSystem, GetCurrentPresetActions());
    }

    private void CommitLayout()
    {
        RebuildLayout();
        UpdateActionBar();
        SaveLayout();
    }

    private void ReserveSlot(int slot, EntityUid action)
    {
        if (GetActionReservationKey(action) is not { } key)
            return;

        _hiddenActions.Remove(key);
        RemoveKeyReservation(key);

        if (!_savedLayout.TryGetValue(slot, out var list))
            _savedLayout[slot] = list = new List<string>();

        list.Insert(0, key);
    }

    private void HideAction(EntityUid? action)
    {
        if (GetActionReservationKey(action) is not { } key)
            return;

        RemoveKeyReservation(key);
        _hiddenActions.Add(key);
    }

    private void AddSlotReservation(int slot, string key)
    {
        if (!_savedLayout.TryGetValue(slot, out var list))
            _savedLayout[slot] = list = new List<string>();

        if (!list.Contains(key))
            list.Add(key);
    }

    private void RemoveKeyReservation(string key)
    {
        var emptySlots = new List<int>();
        foreach (var (slot, list) in _savedLayout)
        {
            list.Remove(key);
            if (list.Count == 0)
                emptySlots.Add(slot);
        }

        foreach (var slot in emptySlots)
        {
            _savedLayout.Remove(slot);
        }
    }

    private string? GetActionReservationKey(EntityUid? action)
    {
        if (_actionsSystem == null || !_actionsSystem.TryGetActionData(action, out var comp, false))
            return null;

        return GetActionReservationKey(action.Value, comp);
    }

    private string? GetActionReservationKey(EntityUid action, BaseActionComponent? comp)
    {
        if (!EntityManager.TryGetComponent<MetaDataComponent>(action, out var meta)
            || meta.EntityPrototype?.ID is not { } actionProto)
        {
            return null;
        }

        if (comp?.Container is not { } container || container == _playerManager.LocalEntity)
            return actionProto;

        if (!EntityManager.TryGetComponent<MetaDataComponent>(container, out var containerMeta)
            || containerMeta.EntityPrototype?.ID is not { } providerProto)
        {
            return actionProto;
        }

        return $"{actionProto}|{providerProto}";
    }

    private void RebuildLayout()
    {
        if (_actionsSystem == null)
            return;

        var restored = new EntityUid?[TotalSlots];
        var used = new HashSet<EntityUid>();
        var available = new List<(EntityUid Id, BaseActionComponent Comp, string? Key)>();
        var byKey = new Dictionary<string, Queue<EntityUid>>();

        foreach (var (id, comp) in _actionsSystem.GetClientActions())
        {
            var key = GetActionReservationKey(id, comp);
            available.Add((id, comp, key));

            if (key == null)
                continue;

            if (!byKey.TryGetValue(key, out var queue))
                byKey[key] = queue = new Queue<EntityUid>();

            queue.Enqueue(id);
        }

        foreach (var (slot, keys) in _savedLayout.OrderBy(x => x.Key))
        {
            foreach (var key in keys)
            {
                if (!byKey.TryGetValue(key, out var queue) || !queue.TryDequeue(out var id))
                    continue;

                var target = slot;
                while (target < TotalSlots && restored[target] != null)
                    target++;

                if (target >= TotalSlots)
                    continue;

                restored[target] = id;
                used.Add(id);
            }
        }

        available.Sort((a, b) => ActionComparer((a.Id, a.Comp), (b.Id, b.Comp)));

        foreach (var (id, comp, key) in available)
        {
            if (used.Contains(id) || !comp.AutoPopulate || key != null && _hiddenActions.Contains(key))
                continue;

            var target = FindAutoSlot(restored);
            if (target < 0)
                break;

            restored[target] = id;
            used.Add(id);
        }

        _actions.Clear();
        _actions.AddRange(restored);
    }

    private int FindAutoSlot(EntityUid?[] restored)
    {
        for (var i = 0; i < restored.Length; i++)
        {
            if (restored[i] == null && !_savedLayout.ContainsKey(i))
                return i;
        }

        for (var i = 0; i < restored.Length; i++)
        {
            if (restored[i] == null)
                return i;
        }

        return -1;
    }

    private void InitializeSavedLayoutFromCurrent()
    {
        _savedLayout.Clear();
        _hiddenActions.Clear();

        for (var i = 0; i < _actions.Count && i < TotalSlots; i++)
        {
            if (GetActionReservationKey(_actions[i]) is { } key)
                AddSlotReservation(i, key);
        }
    }

    private string? GetMobProfileKey()
    {
        if (!EntityManager.TryGetComponent<MetaDataComponent>(_playerManager.LocalEntity, out var meta))
            return null;

        return meta.EntityPrototype?.ID;
    }

    private ResPath GetLayoutPath()
    {
        var serverId = SanitizeFileName(_cfg.GetCVar(CCVars.ServerId));
        var name = string.IsNullOrWhiteSpace(_currentProfileKey)
            ? serverId
            : $"{serverId}_{SanitizeFileName(_currentProfileKey)}";

        return new ResPath($"{LayoutDirectory}/{name}.txt");
    }

    private static string SanitizeFileName(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray();
        return chars.Length == 0 ? "default" : new string(chars);
    }

    private void LoadLayout()
    {
        _savedLayout.Clear();
        _hiddenActions.Clear();
        _layoutLoaded = true;

        try
        {
            var path = GetLayoutPath();
            if (!_res.UserData.Exists(path))
            {
                InitializeSavedLayoutFromCurrent();
                return;
            }

            using var reader = _res.UserData.OpenText(path);
            while (reader.ReadLine() is { } line)
            {
                var split = line.Split('\t', 2);
                if (split.Length != 2 || !int.TryParse(split[0], out var slot))
                    continue;

                var key = split[1].Trim();
                if (key.Length == 0)
                    continue;

                if (slot == HiddenSlot)
                    _hiddenActions.Add(key);
                else if (slot >= 0 && slot < TotalSlots)
                    AddSlotReservation(slot, key);
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to load actions layout: {e.Message}");
        }
    }

    private void SaveLayout()
    {
        if (!_layoutLoaded)
            return;

        try
        {
            var path = GetLayoutPath();
            _res.UserData.CreateDir(path.Directory);

            using var writer = _res.UserData.OpenWriteText(path);
            foreach (var (slot, keys) in _savedLayout.OrderBy(x => x.Key))
            {
                foreach (var key in keys)
                {
                    writer.WriteLine($"{slot}\t{key}");
                }
            }

            foreach (var key in _hiddenActions)
            {
                writer.WriteLine($"{HiddenSlot}\t{key}");
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to save actions layout: {e.Message}");
        }
    }
}

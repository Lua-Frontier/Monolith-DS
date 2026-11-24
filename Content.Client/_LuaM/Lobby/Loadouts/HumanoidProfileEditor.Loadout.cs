using System.Linq;
using Content.Client._LuaM.Lobby.Loadouts;
using Content.Shared.Clothing;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private const int LoadoutTabIndex = 2;

    private LoadoutEditor? _loadoutEditor;
    private JobPrototype? _loadoutJob;
    private RoleLoadout? _loadoutRole;

    private bool LoadoutTabActive => _loadoutEditor != null && TabContainer.CurrentTab == _loadoutEditor.GetPositionInParent();

    private void InitLoadoutEditor()
    {
        _loadoutEditor = new LoadoutEditor();
        TabContainer.AddChild(_loadoutEditor);
        _loadoutEditor.SetPositionInParent(LoadoutTabIndex);
        TabContainer.SetTabTitle(_loadoutEditor, Loc.GetString("humanoid-profile-editor-loadout-tab"));

        TabContainer.OnTabChanged += _ => UpdateLoadoutTabState();
        _loadoutEditor.OnRoleSelected += SelectLoadoutRole;

        _loadoutEditor.OnLoadoutPressed += (group, loadout) =>
        {
            _loadoutRole?.AddLoadout(group, loadout, _prototypeManager);
            ApplyLoadoutChange();
        };

        _loadoutEditor.OnLoadoutUnpressed += (group, loadout) =>
        {
            _loadoutRole?.RemoveLoadout(group, loadout, _prototypeManager);
            ApplyLoadoutChange();
        };

        _loadoutEditor.OnNameChanged += name =>
        {
            if (_loadoutRole == null || Profile == null)
                return;

            _loadoutRole.EntityName = name;
            Profile = Profile.WithLoadout(_loadoutRole);
            SetDirty();
        };
    }

    private void OpenLoadoutTab(JobPrototype job)
    {
        if (_loadoutEditor == null)
            return;

        SelectLoadoutRole(job);
        TabContainer.CurrentTab = _loadoutEditor.GetPositionInParent();
    }

    private void UpdateLoadoutTabState()
    {
        var active = LoadoutTabActive;
        UpdatePreviewVisibility();

        var job = active ? _loadoutJob : null;
        if (JobOverride == job)
            return;

        JobOverride = job;
        ReloadPreview();
    }

    private void RefreshLoadoutEditor()
    {
        if (_loadoutEditor == null || Profile == null)
            return;

        var departments = _prototypeManager.EnumeratePrototypes<DepartmentPrototype>()
            .Where(d => !d.EditorHidden)
            .ToList();
        departments.Sort(DepartmentUIComparer.Instance);

        var seen = new HashSet<string>();
        var list = new List<(string, List<JobPrototype>)>();
        foreach (var department in departments)
        {
            var jobs = department.Roles
                .Select(id => _prototypeManager.Index(id))
                .Where(job => job.SetPreference
                              && _prototypeManager.HasIndex<RoleLoadoutPrototype>(LoadoutSystem.GetJobPrototype(job.ID))
                              && seen.Add(job.ID))
                .ToList();

            if (jobs.Count == 0)
                continue;

            jobs.Sort(JobUIComparer.Instance);
            list.Add((Loc.GetString(department.Name), jobs));
        }

        _loadoutEditor.SetRoles(list);

        var all = list.SelectMany(d => d.Item2).ToList();
        var selected = all.FirstOrDefault(j => j.ID == _loadoutJob?.ID)
                       ?? all.FirstOrDefault(j => Profile.JobPriorities.TryGetValue(j.ID, out var prio) && prio == JobPriority.High)
                       ?? all.FirstOrDefault();

        if (selected != null)
            SelectLoadoutRole(selected, false);
    }

    private void SelectLoadoutRole(JobPrototype job)
    {
        SelectLoadoutRole(job, true);
    }

    private void SelectLoadoutRole(JobPrototype job, bool reload)
    {
        var session = _playerManager.LocalSession;
        var roleId = LoadoutSystem.GetJobPrototype(job.ID);
        if (_loadoutEditor == null || Profile == null || session == null
            || !_prototypeManager.TryIndex<RoleLoadoutPrototype>(roleId, out var roleProto))
            return;

        Profile.Loadouts.TryGetValue(roleId, out var loadout);
        loadout = loadout?.Clone();
        if (loadout == null)
        {
            loadout = new RoleLoadout(roleProto.ID);
            loadout.SetDefault(Profile, session, _prototypeManager);
        }

        _loadoutJob = job;
        _loadoutRole = loadout;
        _loadoutEditor.SetRole(job, Profile, loadout, roleProto, session, IoCManager.Instance!);

        if (!LoadoutTabActive)
            return;

        JobOverride = job;
        if (reload)
            ReloadPreview();
    }

    private void ApplyLoadoutChange()
    {
        var session = _playerManager.LocalSession;
        if (_loadoutEditor == null || _loadoutRole == null || Profile == null || session == null)
            return;

        Profile = Profile.WithLoadout(_loadoutRole);
        _loadoutEditor.RefreshSlots(Profile, _loadoutRole, session, IoCManager.Instance!);
        ReloadPreview();
    }
}

using System.Numerics;
using Content.Client._LuaM.UserInterface.Hud;
using Content.Client.Stylesheets;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private const float EditorShare = 0.5f;
    private const float EditorMinWidth = 700;
    private const float EditorMaxWidth = 1000;

    private void ApplyPreviewScale()
    {
        if (Profile == null || !_entManager.TryGetComponent<SpriteComponent>(PreviewDummy, out var sprite))
        {
            ReloadPreview();
            return;
        }

        var width = Profile.Appearance.Width <= 0.005f ? 1f : Profile.Appearance.Width;
        var height = Profile.Appearance.Height <= 0.005f ? 1f : Profile.Appearance.Height;
        _entManager.System<SpriteSystem>().SetScale((PreviewDummy, sprite), new Vector2(width, height));
    }

    private void InitHudStyle()
    {
        var rail = new HudTabRail(TabContainer, horizontal: true) { IconFor = TabIcon, Margin = new Thickness(0) };
        TabStripSlot.AddChild(rail);

        SetHudClass(LobbyHudStyles.Positive, SaveButton);
        SetHudClass(LobbyHudStyles.Danger, ResetButton);
        SetHudClass(LobbyHudStyles.Command, ImportButton, ExportButton, ExportImageButton, OpenImagesButton);
        SetHudClass(LobbyHudStyles.Button, RandomizeEverythingButton, HeightResetButton, VoicePlayButton);

        NameEdit.AddStyleClass(LobbyHudStyles.Input);
        AgeEdit.AddStyleClass(LobbyHudStyles.Input);
        HairStylePicker.UseCompactLayout();
        FacialHairPicker.UseCompactLayout();
        foreach (var control in new Control[] { NameEdit, RandomizeEverythingButton, SaveButton, ResetButton })
        {
            control.SetHeight = 34;
            control.VerticalAlignment = Control.VAlignment.Center;
        }

        var skin = new HudColorSection(Loc.GetString("humanoid-profile-editor-section-skin"),
            () => Profile?.Appearance.SkinColor, Skin, RgbSkinColorContainer);
        var eyes = new HudColorSection(Loc.GetString("humanoid-profile-editor-section-eyes"),
            () => Profile?.Appearance.EyeColor, EyeColorPicker);
        ColorsBox.AddChild(skin);
        ColorsBox.AddChild(eyes);
        ColorsWindow.Orphan();
        PreviewPanel.AddChild(ColorsWindow);

        FileMenuButton.OnToggled += args => FileMenu.Visible = args.Pressed;

        var warning = new HudCallout { Status = HudStatus.Warning };
        var warningParent = WarningLabel.Parent!;
        var warningIndex = WarningLabel.GetPositionInParent();
        warning.SetContent(WarningLabel);
        warningParent.AddChild(warning);
        warning.SetPositionInParent(warningIndex);

        _saveState = new HudCallout { HorizontalExpand = false, VerticalAlignment = Control.VAlignment.Center };
        ProfileHighlight.AddChild(_saveState);
        _saveState.SetPositionInParent(FileMenuButton.GetPositionInParent() + 1);

        FileMenu.Orphan();
        var footerParent = ProfileHighlight.Parent!;
        footerParent.AddChild(FileMenu);
        FileMenu.SetPositionInParent(ProfileHighlight.GetPositionInParent());
        HeightResetButton.OnPressed += _ => ResetWidth();
        WidthSlider.AddStyleClass(LobbyHudStyles.SliderClass);

        TabContainer.OnTabChanged += _ => UpdatePreviewVisibility();
        UpdatePreviewVisibility();
    }

    public void SetCharacterHeader(Control header)
    {
        CharacterHeaderSlot.RemoveAllChildren();
        CharacterHeaderSlot.AddChild(header);
    }

    private HudCallout? _saveState;
    private bool? _shownDirty;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_saveState != null && _shownDirty != IsDirty)
        {
            _shownDirty = IsDirty;
            _saveState.Status = IsDirty ? HudStatus.Caution : HudStatus.Nominal;
            _saveState.Lead = Loc.GetString(IsDirty ? "humanoid-profile-editor-state-dirty" : "humanoid-profile-editor-state-saved");
        }

        if (!PreviewPanel.Visible)
        {
            if (!EditorWindow.HorizontalExpand)
                EditorWindow.HorizontalExpand = true;
            if (!float.IsNaN(EditorWindow.SetWidth))
                EditorWindow.SetWidth = float.NaN;
            return;
        }

        if (EditorWindow.HorizontalExpand)
            EditorWindow.HorizontalExpand = false;
        var width = Math.Clamp(Width * EditorShare, EditorMinWidth, EditorMaxWidth);
        if (!MathHelper.CloseTo(EditorWindow.SetWidth, width))
            EditorWindow.SetWidth = width;

        var hasHair = HairStylePicker.Visible || FacialHairPicker.Visible;
        if (HairPanel.Visible != hasHair)
            HairPanel.Visible = hasHair;

        var room = MathF.Min(SpriteBox.Height / 40f, SpriteBox.Width / 34f);
        var scale = MathF.Max(4, MathF.Floor(room));
        if (!MathHelper.CloseTo(SpriteView.Scale.X, scale))
            SpriteView.Scale = new System.Numerics.Vector2(scale, scale);
    }

    private void UpdatePreviewVisibility()
    {
        var current = TabContainer.CurrentTab >= 0 && TabContainer.CurrentTab < TabContainer.ChildCount
            ? TabContainer.GetChild(TabContainer.CurrentTab)
            : null;
        var wide = LoadoutTabActive
                   || current != null && current == JobList.Parent?.Parent
                   || current != null && current == _traitsEditor?.Parent;

        PreviewPanel.Visible = !wide;
        ColorsWindow.Visible = current != null && Contains(current, SpeciesButton);
    }

    private HudIconKind? TabIcon(Control tab)
    {
        if (Contains(tab, SpeciesButton))
            return HudIconKind.Person;
        if (Contains(tab, JobList))
            return HudIconKind.Briefcase;
        if (tab == _loadoutEditor)
            return HudIconKind.Shirt;
        if (_traitsEditor != null && Contains(tab, _traitsEditor))
            return HudIconKind.Star;
        if (Contains(tab, CompanyButton))
            return HudIconKind.Flag;
        if (tab == MarkingsTab)
            return HudIconKind.Palette;
        if (_flavorText != null && Contains(tab, _flavorText))
            return HudIconKind.Rules;
        return null;
    }

    private static bool Contains(Control tab, Control control)
    {
        for (Control? c = control; c != null; c = c.Parent)
        {
            if (c == tab)
                return true;
        }

        return false;
    }

    private static void SetHudClass(string styleClass, params BaseButton[] buttons)
    {
        foreach (var button in buttons)
        {
            button.StyleClasses.Clear();
            button.AddStyleClass(ContainerButton.StyleClassButton);
            button.AddStyleClass(styleClass);
        }
    }
}

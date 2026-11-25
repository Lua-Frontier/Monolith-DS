using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Client._LuaM.UserInterface.Hud;

namespace Content.Client._LuaM.Lobby.Loadouts;

public sealed partial class LoadoutSlotButton : BoxContainer
{
    [Dependency] private IEntityManager _entManager = default!;

    private readonly TextureRect _background;
    private readonly SpriteView _sprite;
    private readonly string _slotTexture;
    private EntProtoId? _item;
    private EntityUid? _entity;

    public ContainerButton Button { get; }

    public const float LabeledWidth = 110;

    public LoadoutSlotButton(string? title, string slotTexture, float size = 64)
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        Margin = new Thickness(0, 4);
        _slotTexture = slotTexture;

        if (title != null)
            SetWidth = LabeledWidth;

        Button = new ContainerButton
        {
            ToggleMode = true,
            SetSize = new Vector2(size, size),
            HorizontalAlignment = HAlignment.Center,
            StyleBoxOverride = Frame(false),
            ModulateSelfOverride = Color.White,
        };

        _background = new TextureRect { Stretch = TextureRect.StretchMode.Scale, MouseFilter = MouseFilterMode.Ignore, Modulate = Color.White.WithAlpha(0.45f), Margin = new Thickness(6) };
        _sprite = new SpriteView
        {
            Scale = new Vector2(size / 32f, size / 32f),
            SetSize = new Vector2(size, size),
            MouseFilter = MouseFilterMode.Ignore,
        };

        Button.AddChild(_background);
        Button.AddChild(_sprite);
        Button.OnMouseEntered += _ => UpdateHighlight(true);
        Button.OnMouseExited += _ => UpdateHighlight(false);
        Button.OnToggled += _ => UpdateHighlight();
        AddChild(Button);

        if (title != null)
        {
            AddChild(new Label
            {
                Text = title,
                HorizontalExpand = true,
                Align = Label.AlignMode.Center,
                ClipText = true,
                StyleClasses = { "LabelSmall" },
            });
        }

        UpdateTextures();
    }

    public void SetLocked(bool locked)
    {
        Button.Disabled = locked;
    }

    public void SetPressed(bool pressed)
    {
        Button.Pressed = pressed;
        UpdateHighlight();
    }

    public void SetItem(EntProtoId? item, string tooltip)
    {
        Button.ToolTip = tooltip;

        if (item != _item)
        {
            _item = item;
            DeleteEntity();

            if (IsInsideTree)
                SpawnEntity();
        }

        UpdateTextures();
    }

    private void UpdateTextures()
    {
        var icon = _item == null ? Theme.ResolveTextureOrNull("Slots/" + _slotTexture)?.Texture : null;
        _background.Texture = icon == null ? null : new AtlasTexture(icon, UIBox2.FromDimensions(new Vector2(4, 4), icon.Size - new Vector2(8, 8)));
    }

    private void UpdateHighlight(bool? hovered = null)
    {
        Button.StyleBoxOverride = Frame(Button.Pressed || (hovered ?? Button.IsHovered));
    }

    private static StyleBoxFlat Frame(bool active)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = active ? HudPalette.OrangeDark : HudPalette.Panel,
            BorderColor = active ? HudPalette.Orange : HudPalette.Border,
            BorderThickness = new Thickness(active ? 2 : 1),
        };
    }

    protected override void OnThemeUpdated()
    {
        base.OnThemeUpdated();
        UpdateTextures();
    }

    private void SpawnEntity()
    {
        if (_item == null || _entity != null)
            return;

        _entity = _entManager.SpawnEntity(_item, MapCoordinates.Nullspace);
        _sprite.SetEntity(_entity);
    }

    private void DeleteEntity()
    {
        _sprite.SetEntity(null);
        _entManager.DeleteEntity(_entity);
        _entity = null;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        SpawnEntity();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        DeleteEntity();
    }
}

using System.Numerics;
using Content.Client.ContextMenu.UI;
using Content.Shared._LuaM.HoloComms;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Shuttles.UI;

public sealed class RadarContextMenu : Popup
{
    private static readonly Color DisabledColor = Color.FromHex("#7A7A7A");

    private readonly BoxContainer _body = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly RadarContextMenu? _parent;
    private RadarContextMenu? _subMenu;

    public RadarContextMenu(RadarContextMenu? parent = null)
    {
        _parent = parent;

        var panel = new PanelContainer();
        panel.SetOnlyStyleClass(ContextMenuPopup.StyleClassContextMenuPopup);
        panel.AddChild(_body);
        AddChild(panel);

        UserInterfaceManager.ModalRoot.AddChild(this);
        OnPopupHide += CloseSubMenu;
    }

    public void OpenAt(Vector2 position)
    {
        Open(UIBox2.FromDimensions(position, new Vector2(1, 1)));
    }

    public void AddHeader(string text)
    {
        var label = new RichTextLabel { Margin = new Thickness(6, 4, 6, 2) };
        label.SetMessage(FormattedMessage.FromMarkupPermissive($"[bold]{FormattedMessage.EscapeText(text)}[/bold]"));
        _body.AddChild(label);
    }

    public void AddItem(string text, Action? onPressed, string? toolTip = null)
    {
        var item = new RadarContextMenuElement(Format(text, onPressed != null), false)
        {
            Disabled = onPressed == null,
            ToolTip = toolTip,
        };
        item.OnMouseEntered += _ => CloseSubMenu();

        if (onPressed != null)
        {
            item.OnPressed += _ =>
            {
                onPressed();
                CloseAll();
            };
        }

        _body.AddChild(item);
    }

    public void AddSubMenu(string text, Action<RadarContextMenu> fill)
    {
        var item = new RadarContextMenuElement(Format(text, true), true);
        item.OnMouseEntered += _ => OpenSubMenu(item, fill);
        item.OnPressed += _ => OpenSubMenu(item, fill);
        _body.AddChild(item);
    }

    public void AddCallItem(List<HoloCommsContact> calls, Action<NetEntity> onCall)
    {
        if (!calls.Exists(call => !call.Busy))
        {
            AddItem(Loc.GetString("shuttle-radar-menu-unreachable"), null);
            return;
        }

        if (calls.Count == 1 && calls[0].Kind == HoloCommsContactKind.Ship)
        {
            var target = calls[0].Target;
            AddItem(Loc.GetString("shuttle-radar-menu-call"), () => onCall(target));
            return;
        }

        AddSubMenu(Loc.GetString("shuttle-radar-menu-call"), subMenu =>
        {
            foreach (var call in calls)
            {
                var target = call.Target;
                subMenu.AddItem(call.Name, call.Busy ? null : () => onCall(target),
                    call.Busy ? Loc.GetString("shuttle-radar-menu-unreachable") : null);
            }
        });
    }

    private void OpenSubMenu(Control item, Action<RadarContextMenu> fill)
    {
        if (_subMenu is { Visible: true })
            return;

        CloseSubMenu();

        _subMenu = new RadarContextMenu(this);
        fill(_subMenu);

        var altPos = item.GlobalPosition;
        var pos = altPos + new Vector2(item.Width + 2 * ContextMenuElement.ElementMargin, -2 * ContextMenuElement.ElementMargin);
        _subMenu.Open(UIBox2.FromDimensions(pos, new Vector2(1, 1)), altPos);
        _subMenu.SetPositionLast();
    }

    private void CloseSubMenu()
    {
        if (_subMenu == null)
            return;

        var subMenu = _subMenu;
        _subMenu = null;
        subMenu.Close();
        subMenu.Orphan();
    }

    private void CloseAll()
    {
        if (_parent != null)
        {
            _parent.CloseAll();
            return;
        }

        Close();
    }

    private static string Format(string text, bool enabled)
    {
        var escaped = FormattedMessage.EscapeText(text);
        return enabled ? escaped : $"[color={DisabledColor.ToHex()}]{escaped}[/color]";
    }

    private sealed class RadarContextMenuElement : ContextMenuElement
    {
        public RadarContextMenuElement(string text, bool subMenu) : base(text)
        {
            Icon.Visible = false;
            ExpansionIndicator.Visible = subMenu;
        }
    }
}

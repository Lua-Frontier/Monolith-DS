using Content.Shared._LuaM.HoloComms;
using Content.Shared.Telephone;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Shuttles.UI;

/// <summary>
/// The comms button on top of the weapons panel, it slowly pulses while the ship is being called.
/// </summary>
public sealed partial class NavScreen
{
    private static readonly Color HoloCommsRingColor = Color.FromHex("#4EC6E8");
    private static readonly Color HoloCommsLineColor = Color.FromHex("#6FD38A");
    private static readonly Color HoloCommsBorderColor = Color.FromHex("#2D4757");
    private static readonly Color HoloCommsBackgroundColor = Color.FromHex("#111923F0");
    private static readonly Color HoloCommsRingBackgroundColor = Color.FromHex("#183A4FF5");

    /// <summary>
    /// Seconds of one pulse and how far it goes towards the ring colors, it stays soft.
    /// </summary>
    private const double HoloCommsPulsePeriod = 1.6;
    private const float HoloCommsPulseStrength = 0.8f;

    public event Action? OnHoloCommsPressed;

    private bool _holoCommsAvailable;
    private TelephoneState? _holoCommsState;

    // A tint through ModulateSelf only darkened the dark button and left its text as it was
    private readonly StyleBoxFlat _holoCommsStyle = new()
    {
        BorderThickness = new Thickness(1),
        ContentMarginTopOverride = 3,
        ContentMarginBottomOverride = 3,
        ContentMarginLeftOverride = 6,
        ContentMarginRightOverride = 6,
    };

    private static readonly Color HoloCommsRemoteColor = Color.FromHex("#C9D6E2");

    private readonly RichTextLabel _holoCommsRemote = new()
    {
        HorizontalAlignment = HAlignment.Center,
        MouseFilter = MouseFilterMode.Ignore,
        Visible = false,
    };

    private string? _holoCommsRemoteName;

    private void InitializeHoloComms()
    {
        HoloCommsButton.OnPressed += _ => OnHoloCommsPressed?.Invoke();

        var label = HoloCommsButton.Label;
        label.Orphan();

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MouseFilter = MouseFilterMode.Ignore,
        };
        content.AddChild(label);
        content.AddChild(_holoCommsRemote);
        HoloCommsButton.AddChild(content);
    }

    private void UpdateHoloCommsRemote(string? remote)
    {
        if (remote == _holoCommsRemoteName)
            return;

        _holoCommsRemoteName = remote;
        _holoCommsRemote.Visible = remote != null;

        if (remote == null)
            return;

        var message = new FormattedMessage();
        message.PushColor(HoloCommsRemoteColor);
        message.AddText(remote);
        message.Pop();
        _holoCommsRemote.SetMessage(message);
    }

    private void UpdateHoloCommsButton()
    {
        var available = _entManager.TryGetComponent<HoloCommsConsoleComponent>(_consoleEntity, out var comms);
        if (available != _holoCommsAvailable)
        {
            _holoCommsAvailable = available;
            HoloCommsButton.Visible = available;
            UpdateWeaponsPanelVisibility();
        }

        if (comms == null)
            return;

        var state = TelephoneState.Idle;
        if (_entManager.TryGetComponent<TelephoneComponent>(comms.Module, out var telephone))
            state = telephone.CurrentState;

        if (state != _holoCommsState)
        {
            _holoCommsState = state;
            HoloCommsButton.Text = Loc.GetString(state switch
            {
                TelephoneState.Ringing => "holo-comms-button-ringing",
                TelephoneState.Calling => "holo-comms-button-calling",
                TelephoneState.InCall => "holo-comms-button-in-call",
                _ => "holo-comms-button",
            }).ToUpperInvariant();
        }

        UpdateHoloCommsRemote(state == TelephoneState.Idle ? null : comms.RemoteName);

        // Only the colors change every frame, the layout stays as it is
        switch (state)
        {
            case TelephoneState.Ringing:
                var pulse = HoloCommsPulseStrength * (1f - MathF.Cos((float) (_timing.RealTime.TotalSeconds / HoloCommsPulsePeriod * Math.Tau))) / 2f;
                SetHoloCommsColors(
                    Color.InterpolateBetween(HoloCommsBorderColor, HoloCommsRingColor, pulse),
                    Color.InterpolateBetween(HoloCommsBackgroundColor, HoloCommsRingBackgroundColor, pulse),
                    Color.InterpolateBetween(Color.White, HoloCommsRingColor, pulse));
                break;
            case TelephoneState.InCall:
                SetHoloCommsColors(HoloCommsLineColor, HoloCommsBackgroundColor, HoloCommsLineColor);
                break;
            default:
                HoloCommsButton.StyleBoxOverride = null;
                HoloCommsButton.Label.FontColorOverride = null;
                break;
        }
    }

    private void SetHoloCommsColors(Color border, Color background, Color text)
    {
        _holoCommsStyle.BorderColor = border;
        _holoCommsStyle.BackgroundColor = background;
        HoloCommsButton.StyleBoxOverride = _holoCommsStyle;
        HoloCommsButton.Label.FontColorOverride = text;
    }
}

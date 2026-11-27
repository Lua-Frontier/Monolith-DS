using Content.Client.Options.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Client._LuaM.Options;

/// <summary>
/// UI scale as a percent slider instead of fixed presets. The automatic scale stays available as a checkbox,
/// the slider is locked while it is on. The CVar keeps 0 for automatic and the scale factor otherwise.
/// </summary>
public sealed class OptionUIScale : BaseOptionCVar<float>
{
    public const int MinPercent = 75;
    public const int MaxPercent = 200;

    private readonly CheckBox _auto;
    private readonly IUserInterfaceManager _ui;
    private readonly OptionSlider _slider;

    protected override float Value
    {
        get => _auto.Pressed ? 0f : _slider.Slider.Value / 100f;
        set
        {
            _auto.Pressed = value <= 0f;
            var percent = _auto.Pressed ? _ui.DefaultUIScale * 100f : value * 100f;
            _slider.Slider.Value = Math.Clamp(MathF.Round(percent), MinPercent, MaxPercent);
            UpdateControls();
        }
    }

    public OptionUIScale(OptionsTabControlRow controller, IConfigurationManager cfg, CheckBox auto, OptionSlider slider)
        : base(controller, cfg, CVars.DisplayUIScale)
    {
        _ui = IoCManager.Resolve<IUserInterfaceManager>();
        _auto = auto;
        _slider = slider;

        slider.Slider.MinValue = MinPercent;
        slider.Slider.MaxValue = MaxPercent;
        slider.Slider.Rounded = true;
        auto.Text = Loc.GetString("ui-options-scale-auto", ("scale", _ui.DefaultUIScale));

        slider.Slider.OnValueChanged += _ =>
        {
            UpdateControls();
            ValueChanged();
        };
        auto.OnToggled += _ =>
        {
            UpdateControls();
            ValueChanged();
        };
    }

    private void UpdateControls()
    {
        _slider.Slider.Disabled = _auto.Pressed;
        _slider.Slider.ModulateSelfOverride = _auto.Pressed ? Color.Gray : null;
        _slider.ValueLabel.Text = $"{(int) _slider.Slider.Value}%";
    }
}

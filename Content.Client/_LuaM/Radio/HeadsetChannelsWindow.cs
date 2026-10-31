using Content.Client.UserInterface.Controls;
using Content.Shared._LuaM.Radio;
using Content.Shared.Radio;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Radio;

public sealed partial class HeadsetChannelsWindow : FancyWindow
{
    [Dependency] private IPrototypeManager _proto = default!;

    public event Action<string, bool>? OnChannelToggled;
    public event Action<int>? OnFrequencySet;

    private readonly BoxContainer _channels;
    private readonly Label _empty;
    private readonly BoxContainer _frequencyRow;
    private readonly LineEdit _frequencyEdit;
    private readonly Label _frequencyHint;

    public HeadsetChannelsWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("headset-channels-window-title");
        MinWidth = 260;

        _channels = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
        };

        _empty = new Label
        {
            Text = Loc.GetString("headset-channels-window-empty"),
            Margin = new Thickness(8),
            Visible = false,
        };

        _frequencyEdit = new LineEdit { MinWidth = 70 };
        var setButton = new Button { Text = Loc.GetString("headset-tunable-radio-set") };
        _frequencyHint = new Label { FontColorOverride = Color.Gray, Margin = new Thickness(4, 0, 0, 0) };

        _frequencyEdit.OnTextEntered += _ => SubmitFrequency();
        setButton.OnPressed += _ => SubmitFrequency();

        _frequencyRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(8, 0, 8, 8),
            Visible = false,
        };
        _frequencyRow.AddChild(new Label { Text = Loc.GetString("headset-tunable-radio-frequency"), Margin = new Thickness(0, 0, 4, 0) });
        _frequencyRow.AddChild(_frequencyEdit);
        _frequencyRow.AddChild(setButton);
        _frequencyRow.AddChild(_frequencyHint);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        root.AddChild(_frequencyRow);
        root.AddChild(_channels);
        root.AddChild(_empty);
        ContentsContainer.AddChild(root);
    }

    private void SubmitFrequency()
    {
        _frequencyEdit.ReleaseKeyboardFocus();

        if (int.TryParse(_frequencyEdit.Text, out var frequency))
            OnFrequencySet?.Invoke(frequency);
    }

    public void UpdateState(HeadsetChannelsBoundUserInterfaceState state)
    {
        _frequencyRow.Visible = state.Frequency != null;
        if (state.Frequency is { } freq)
        {
            if (!_frequencyEdit.HasKeyboardFocus())
                _frequencyEdit.Text = freq.ToString();

            _frequencyHint.Text = Loc.GetString("headset-tunable-radio-range",
                ("min", state.MinFrequency), ("max", state.MaxFrequency));
        }

        _channels.RemoveAllChildren();
        _empty.Visible = state.Channels.Count == 0;

        foreach (var channel in state.Channels)
        {
            var name = channel;
            var color = Color.White;
            if (_proto.TryIndex<RadioChannelPrototype>(channel, out var proto))
            {
                name = proto.LocalizedName;
                color = proto.Color;
            }

            var checkBox = new CheckBox
            {
                Text = name,
                Pressed = !state.DisabledChannels.Contains(channel),
            };
            checkBox.Label.FontColorOverride = color;

            var id = channel;
            checkBox.OnToggled += args => OnChannelToggled?.Invoke(id, args.Pressed);

            _channels.AddChild(checkBox);
        }
    }
}

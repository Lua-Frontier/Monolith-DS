using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class HudStyleScope : Control
{
    public const string Skip = "LuaMHudSkip";

    private static readonly StyleBoxMarker ColorGrabber = CreateColorGrabber();

    private static readonly AttachedProperty<string?> Written = AttachedProperty<string?>.CreateNull("LuaMHudWritten", typeof(HudStyleScope));

    /// <summary>
    /// Set on every control the scope already listens to, so it subscribes to it only once.
    /// </summary>
    private static readonly AttachedProperty<bool> Hooked = AttachedProperty<bool>.Create("LuaMHudHooked", typeof(HudStyleScope));

    /// <summary>
    /// A full walk also runs this often, for style classes code changes on existing controls.
    /// </summary>
    private const float SafetyWalkInterval = 1f;

    // The whole lobby used to be walked every frame. Styling a control is needed once, when it appears or shows up;
    // only the tracked captions are rewritten whenever their text changes, so only they are checked every frame.
    private readonly List<Control> _tracked = new();
    private readonly Action<Control> _markDirty;
    private bool _dirty = true;
    private float _sinceWalk;

    public HudStyleScope()
    {
        Visible = true;
        MouseFilter = MouseFilterMode.Ignore;
        _markDirty = _ => _dirty = true;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (Parent == null)
            return;

        _sinceWalk += args.DeltaSeconds;
        if (_dirty || _sinceWalk >= SafetyWalkInterval)
        {
            _dirty = false;
            _sinceWalk = 0;
            _tracked.Clear();
            Apply(Parent, this);
            return;
        }

        foreach (var control in _tracked)
        {
            ApplyText(control);
        }
    }

    public static void Apply(Control control)
    {
        Apply(control, null);
    }

    private static void Apply(Control control, HudStyleScope? scope)
    {
        if (scope != null && !control.GetValue(Hooked))
        {
            // a new child or a hidden part showing up needs styling, everything else stays styled
            control.SetValue(Hooked, true);
            control.OnChildAdded += scope._markDirty;
            control.OnVisibilityChanged += scope._markDirty;
        }

        if (!control.Visible || control.HasStyleClass(Skip))
            return;

        switch (control)
        {
            case OptionButton option:
                Tag(option, LobbyHudStyles.Option);
                foreach (var part in option.Children)
                {
                    if (part is BoxContainer row)
                    {
                        foreach (var item in row.Children)
                        {
                            if (item is TextureRect arrow && arrow.Margin.Left < 8)
                                arrow.Margin = new Thickness(8, 0, 0, 0);
                        }
                    }
                }
                break;
            case Button button when !HasHudClass(button) && button.StyleBoxOverride == null:
                Tag(button, LobbyHudStyles.Button);
                break;
            case ContainerButton custom when custom.StyleBoxOverride != null && custom.ModulateSelfOverride == null:
                custom.ModulateSelfOverride = Color.White;
                break;
            case LineEdit edit when !HasHudClass(edit):
                Tag(edit, LobbyHudStyles.Input);
                break;
            case Slider slider when slider.GetType() == typeof(Slider):
                Tag(slider, LobbyHudStyles.SliderClass);
                break;
            case ColorableSlider colorSlider:
                Tag(colorSlider, LobbyHudStyles.ColorSliderClass);
                if (colorSlider.BackgroundStyleBoxOverride is ColorSelectorStyleBox gradient && gradient.Texture != Texture.White)
                {
                    gradient.Texture = Texture.White;
                    gradient.SetPatchMargin(StyleBox.Margin.All, 0);
                    colorSlider.GrabberStyleBoxOverride = ColorGrabber;
                    colorSlider.Margin = new Thickness(6, 0, 0, 0);
                    foreach (var part in colorSlider.Children)
                    {
                        part.InvalidateMeasure();
                    }
                }
                break;
            case SpinBox spin:
                if (spin.LineEditControl.MinWidth < 52)
                    spin.LineEditControl.MinWidth = 52;
                break;
            case ItemList list:
                Tag(list, LobbyHudStyles.List);
                break;
            case ScrollBar bar:
                Tag(bar, LobbyHudStyles.Scroll);
                break;
            case Label label when label.FontOverride == null && ((ICollection<string>) label.StyleClasses).Count == 0:
                Tag(label, LobbyHudStyles.Auto);
                break;
        }

        var caption = CaptionOf(control);
        if (caption != Caption.None)
        {
            ApplyText(control, caption);
            scope?._tracked.Add(control);
        }

        foreach (var child in control.Children)
        {
            Apply(child, scope);
        }
    }

    private enum Caption : byte
    {
        None,
        Spaced,
        Heading,
        Lamp,
    }

    private static Caption CaptionOf(Control control)
    {
        switch (control)
        {
            case Label label when label.HasStyleClass(LobbyHudStyles.TabTitle)
                                  || label.HasStyleClass(LobbyHudStyles.Title)
                                  || label.HasStyleClass(LobbyHudStyles.LabelClass):
                return Caption.Spaced;
            case Label label when label.HasStyleClass(LobbyHudStyles.Heading):
                return Caption.Heading;
            case Button button when HasHudClass(button):
                return Caption.Lamp;
            default:
                return Caption.None;
        }
    }

    /// <summary>
    /// Rewrites a caption in the HUD letter case and spacing when its text was changed since the last time.
    /// </summary>
    private static void ApplyText(Control control)
    {
        // tracked controls keep their kind until the next walk, only the text is checked here
        switch (control)
        {
            case Label label when label.HasStyleClass(LobbyHudStyles.Heading):
                ApplyText(label, Caption.Heading);
                break;
            case Label label:
                ApplyText(label, Caption.Spaced);
                break;
            case Button button:
                ApplyText(button, Caption.Lamp);
                break;
        }
    }

    private static void ApplyText(Control control, Caption caption)
    {
        switch (caption)
        {
            case Caption.Spaced when control is Label { Text: { } text } label && !IsWritten(label, text):
                var spaced = Track(text.ToUpperInvariant());
                if (spaced != text)
                    label.Text = spaced;
                Remember(label, label.Text!);
                break;
            case Caption.Heading when control is Label { Text: { } text } label && !IsWritten(label, text):
                var upper = text.Replace(TrackSpace.ToString(), string.Empty).ToUpperInvariant();
                if (upper != text)
                    label.Text = upper;
                Remember(label, label.Text!);
                break;
            case Caption.Lamp when control is Button { Text: { } text } lamp && !IsWritten(lamp, text):
                var legend = Track(text.ToUpperInvariant());
                if (legend != text)
                    lamp.Text = legend;
                Remember(lamp, lamp.Text ?? legend);
                break;
        }
    }

    public const char TrackSpace = ' ';

    public static string Track(string text)
    {
        var plain = text.Replace(TrackSpace.ToString(), string.Empty);
        if (plain.Length < 2)
            return plain;

        var builder = new System.Text.StringBuilder(plain.Length * 2);
        for (var i = 0; i < plain.Length; i++)
        {
            builder.Append(plain[i]);
            if (i < plain.Length - 1 && !char.IsHighSurrogate(plain[i]))
                builder.Append(TrackSpace);
        }

        return builder.ToString();
    }

    private static StyleBoxMarker CreateColorGrabber()
    {
        var box = new StyleBoxMarker();
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, 3);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, 6);
        return box;
    }

    private static bool IsWritten(Control control, string text)
    {
        return ReferenceEquals(control.GetValue(Written), text);
    }

    private static void Remember(Control control, string text)
    {
        control.SetValue(Written, text);
    }

    private static void Tag(Control control, string styleClass)
    {
        if (!control.HasStyleClass(styleClass))
            control.AddStyleClass(styleClass);
    }

    private static bool HasHudClass(Control control)
    {
        foreach (var styleClass in control.StyleClasses)
        {
            if (styleClass.StartsWith("LuaMHud", StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}

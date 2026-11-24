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

    public HudStyleScope()
    {
        Visible = true;
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (Parent == null)
            return;

        Apply(Parent);
        Apply(UserInterfaceManager.ModalRoot);
        Apply(UserInterfaceManager.WindowRoot);
    }

    public static void Apply(Control control)
    {
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
            case Label label when label.Text != null
                                  && (label.HasStyleClass(LobbyHudStyles.TabTitle)
                                      || label.HasStyleClass(LobbyHudStyles.Title)
                                      || label.HasStyleClass(LobbyHudStyles.LabelClass)):
                if (IsWritten(label, label.Text))
                    break;
                var spaced = Track(label.Text.ToUpperInvariant());
                if (spaced != label.Text)
                    label.Text = spaced;
                Remember(label, label.Text);
                break;
            case Label label when label.Text != null && label.HasStyleClass(LobbyHudStyles.Heading):
                if (IsWritten(label, label.Text))
                    break;
                var upper = label.Text.Replace(TrackSpace.ToString(), string.Empty).ToUpperInvariant();
                if (upper != label.Text)
                    label.Text = upper;
                Remember(label, label.Text);
                break;
        }

        if (control is Button { Text: { } text } lamp && !IsWritten(lamp, text) && HasHudClass(lamp))
        {
            var legend = Track(text.ToUpperInvariant());
            if (legend != text)
                lamp.Text = legend;
            Remember(lamp, lamp.Text ?? legend);
        }

        foreach (var child in control.Children)
        {
            Apply(child);
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

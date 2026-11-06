using System.Linq;
using System.Numerics;
using Content.Client.Actions;
using Content.Shared.Input;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Actions.Controls;

[Virtual]
public partial class ActionButtonContainer : GridContainer
{
    // LuaM-start
    public const int SlotsPerLine = 10;
    public const int LinesPerPreset = 2;
    public const int SlotsPerPreset = SlotsPerLine * LinesPerPreset;
    private const float ButtonSize = 64f;
    private static readonly Vector2 OldMaxSize = new(64, 9999);

    private float? _layoutLength;
    private bool _layoutVertical;

    public bool Advanced { get; private set; }
    // LuaM-end

    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IInputManager _input = default!;

    public event Action<GUIBoundKeyEventArgs, ActionButton>? ActionPressed;
    public event Action<GUIBoundKeyEventArgs, ActionButton>? ActionUnpressed;
    public event Action<ActionButton>? ActionFocusExited;

    public ActionButtonContainer()
    {
        IoCManager.InjectDependencies(this);
    }

    public ActionButton this[int index]
    {
        get => (ActionButton) GetChild(index);
    }

    public void SetActionData(ActionsSystem system, params EntityUid?[] actionTypes)
    {
        var uniqueCount = Advanced // LuaM
            ? Math.Max(SlotsPerPreset, actionTypes.Length)
            : Math.Min(system.GetClientActions().Count(), actionTypes.Length + 1);
        var keys = ContentKeyFunctions.GetHotbarBoundKeys();

        for (var i = 0; i < uniqueCount; i++)
        {
            if (i >= ChildCount)
            {
                AddChild(MakeButton(i));
            }

            if (!actionTypes.TryGetValue(i, out var action))
                action = null;
            ((ActionButton) GetChild(i)).UpdateData(action, system);
        }

        for (var i = ChildCount - 1; i >= uniqueCount; i--)
        {
            RemoveChild(GetChild(i));
        }

        ActionButton MakeButton(int index)
        {
            var button = new ActionButton(_entity);

            if (!keys.TryGetValue(index, out var boundKey))
                return button;

            button.KeyBind = boundKey;
            if (_input.TryGetKeyBinding(boundKey, out var binding))
            {
                button.Label.Text = GetCompactKeyString(binding); // LuaM
            }

            // LuaM-start
            button.Label.ClipText = true;
            button.Label.HorizontalAlignment = HAlignment.Stretch;
            // LuaM-end

            return button;
        }
    }

    // LuaM-start
    public void SetAdvanced(bool advanced)
    {
        Advanced = advanced;
        HorizontalAlignment = advanced ? HAlignment.Left : HAlignment.Center;
        MaxSize = advanced ? new Vector2(float.PositiveInfinity, float.PositiveInfinity) : OldMaxSize;
        ApplyLayout();
    }

    public void SetFixedLayout(float maxLength, bool vertical)
    {
        _layoutLength = maxLength;
        _layoutVertical = vertical;
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (_layoutLength is not { } length)
        {
            Rows = Advanced ? SlotsPerLine : 1;
            return;
        }

        if (!Advanced)
        {
            if (length <= 0)
                return;

            if (_layoutVertical)
                MaxGridHeight = length;
            else
                MaxGridWidth = length;

            return;
        }

        var perLine = length >= SlotsPerLine * ButtonSize ? SlotsPerLine : SlotsPerLine / 2;

        if (_layoutVertical)
            Rows = perLine;
        else
            Columns = perLine;
    }

    private string GetCompactKeyString(IKeyBinding binding)
    {
        if (binding.Mod1 == Keyboard.Key.Unknown || binding.Mod2 != Keyboard.Key.Unknown)
            return binding.GetKeyString();

        var key = binding.BaseKey is >= Keyboard.Key.Num0 and <= Keyboard.Key.Num9
            ? ((int) (binding.BaseKey - Keyboard.Key.Num0)).ToString()
            : _input.GetKeyName(binding.BaseKey);

        return $"{_input.GetKeyName(binding.Mod1)}+{key}";
    }
    // LuaM-end

    public void ClearActionData()
    {
        foreach (var button in Children)
        {
            ((ActionButton) button).ClearData();
        }
    }

    protected override void ChildAdded(Control newChild)
    {
        base.ChildAdded(newChild);

        if (newChild is not ActionButton button)
            return;

        button.ActionPressed += ActionPressed;
        button.ActionUnpressed += ActionUnpressed;
        button.ActionFocusExited += ActionFocusExited;
    }

    protected override void ChildRemoved(Control newChild)
    {
        if (newChild is not ActionButton button)
            return;

        button.ActionPressed -= ActionPressed;
        button.ActionUnpressed -= ActionUnpressed;
        button.ActionFocusExited -= ActionFocusExited;
    }

    public bool TryGetButtonIndex(ActionButton button, out int position)
    {
        if (button.Parent != this)
        {
            position = 0;
            return false;
        }

        position = button.GetPositionInParent();
        return true;
    }

    public IEnumerable<ActionButton> GetButtons()
    {
        foreach (var control in Children)
        {
            if (control is ActionButton button)
                yield return button;
        }
    }
}

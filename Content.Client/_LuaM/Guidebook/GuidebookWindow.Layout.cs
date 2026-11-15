/* LuaM - AGPLv3. See AGPLv3.txt. */
using System.Linq;
using System.Numerics;
using Content.Shared._LuaM.Guidebook;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Input;

namespace Content.Client.Guidebook.Controls;

public sealed partial class GuidebookWindow
{
    [Dependency] private IConfigurationManager _layoutConfig = default!;

    private Vector2 _savedWindowSize;
    private float _navigationWidth;
    private bool _layoutInitialized;
    private bool _resizingWindow;

    private void InitializeLayout()
    {
        _savedWindowSize = new Vector2(
            ValidDimension(_layoutConfig.GetCVar(GuidebookCVars.WindowWidth), 900),
            ValidDimension(_layoutConfig.GetCVar(GuidebookCVars.WindowHeight), 700));
        var screen = UserInterfaceManager.WindowRoot.Size;
        if (screen.X > 0 && screen.Y > 0)
        {
            MinSize = Vector2.Min(MinSize, screen);
            _savedWindowSize = Vector2.Min(Vector2.Max(_savedWindowSize, MinSize), screen);
        }
        else
            _savedWindowSize = Vector2.Max(_savedWindowSize, MinSize);
        SetSize = _savedWindowSize;

        _navigationWidth = ValidDimension(_layoutConfig.GetCVar(GuidebookCVars.NavigationWidth), 240);
        // Only this tree stops reporting its longest row as the window's minimum width.
        var treeScroll = Tree.Children.OfType<ScrollContainer>().Single();
        treeScroll.ReturnMeasure = false;
        treeScroll.HScrollEnabled = true;
        Split.OnSplitResizeFinished += SaveNavigationWidth;

        // Reuse the native title row instead of inserting a toolbar above the document.
        var titleRow = (BoxContainer) this.FindControl<Label>("WindowTitle").Parent!;
        ReturnContainer.Orphan();
        titleRow.AddChild(ReturnContainer);
        ReturnContainer.SetPositionInParent(1);
        titleRow.MinHeight = 28;
        ReturnContainer.Margin = new Thickness(4, 0, 6, 0);

        _layoutInitialized = true;
        RestoreNavigationWidth(_savedWindowSize.X);
    }

    private static float ValidDimension(float value, float fallback) =>
        float.IsFinite(value) && value > 0 ? value : fallback;

    private void RestoreNavigationWidth(float width)
    {
        if (width <= 0)
            return;
        var max = Math.Max(165, width - 285);
        var center = Math.Clamp(_navigationWidth, 165, max);
        Split.SetSplitFractionOnNextArrange(center / width);
    }

    protected override void Resized()
    {
        base.Resized();
        if (_layoutInitialized && TreeBox.Visible)
            RestoreNavigationWidth(Size.X);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            var mode = GetDragModeFor(args.RelativePosition);
            _resizingWindow = mode != DragMode.None && mode != DragMode.Move;
        }
        base.KeyBindDown(args);
    }

    protected override void ExitedTree()
    {
        _resizingWindow = false;
        base.ExitedTree();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick || !_resizingWindow)
            return;
        _resizingWindow = false;
        if (Size.X <= 0 || Size.Y <= 0 || Size == _savedWindowSize)
            return;
        _savedWindowSize = Size;
        _layoutConfig.SetCVar(GuidebookCVars.WindowWidth, Size.X);
        _layoutConfig.SetCVar(GuidebookCVars.WindowHeight, Size.Y);
        _layoutConfig.SaveToFile();
    }

    private void SaveNavigationWidth()
    {
        if (!TreeBox.Visible || Split.SplitCenter == _navigationWidth)
            return;
        _navigationWidth = Split.SplitCenter;
        _layoutConfig.SetCVar(GuidebookCVars.NavigationWidth, _navigationWidth);
        _layoutConfig.SaveToFile();
    }
}

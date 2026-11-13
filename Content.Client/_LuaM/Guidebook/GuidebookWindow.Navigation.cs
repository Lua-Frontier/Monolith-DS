/* LuaM - AGPLv3. See AGPLv3.txt. */
using Content.Shared.Guidebook;
using Robust.Shared.Prototypes;

namespace Content.Client.Guidebook.Controls;

public sealed partial class GuidebookWindow
{
    private readonly Stack<ProtoId<GuideEntryPrototype>> _backHistory = new();
    private ProtoId<GuideEntryPrototype>? _currentGuide;
    private bool _goingBack;

    private void ResetNavigationHistory()
    {
        _backHistory.Clear();
        _currentGuide = null;
        _goingBack = false;
        BackButton.Disabled = true;
    }

    private void RecordNavigation(GuideEntry entry)
    {
        if (!_goingBack && _currentGuide is { } previous && previous != entry.Id)
            _backHistory.Push(previous);
        _currentGuide = entry.Id;
        // Back remains available after Home; disabling Home keeps both buttons' geometry stable.
        ReturnContainer.Visible = true;
        BackButton.Disabled = _backHistory.Count == 0;
        HomeButton.Disabled = entry.Id == "ColossusHome";
    }

    private void GoBack()
    {
        if (!_backHistory.TryPop(out var previous))
            return;
        _goingBack = true;
        try
        {
            HandleClick(previous.Id);
        }
        finally
        {
            _goingBack = false;
            BackButton.Disabled = _backHistory.Count == 0;
        }
    }
}

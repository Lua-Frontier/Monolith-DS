using System.Globalization;
using Content.Client._LuaM.WarDeclaration;

namespace Content.Client.Communications.UI;

// LuaM: war declaration button of the faction communications consoles
public sealed partial class CommunicationsConsoleMenu
{
    public bool ShowWarDeclaration;
    public bool WarDeclared;
    public TimeSpan WarDeclarationAvailableAt;

    public event Action<string>? OnDeclareWar;

    private WarDeclarationWindow? _warDeclarationWindow;

    private void InitializeWarDeclaration()
    {
        DeclareWarButton.OnPressed += _ => OpenWarDeclarationWindow();
        OnClose += () => _warDeclarationWindow?.Close();
    }

    private void OpenWarDeclarationWindow()
    {
        if (_warDeclarationWindow is { IsOpen: true })
        {
            _warDeclarationWindow.MoveToFront();
            return;
        }

        _warDeclarationWindow = new WarDeclarationWindow();
        _warDeclarationWindow.OnConfirm += reason => OnDeclareWar?.Invoke(reason);
        _warDeclarationWindow.OpenCentered();
    }

    public void UpdateWarDeclaration()
    {
        DeclareWarButton.Visible = ShowWarDeclaration;
        if (!ShowWarDeclaration)
            return;

        if (WarDeclared)
        {
            DeclareWarButton.Disabled = true;
            DeclareWarButton.Text = Loc.GetString("comms-console-menu-declare-war-button-declared");
            return;
        }

        var left = WarDeclarationAvailableAt - _timing.CurTime;
        if (left > TimeSpan.Zero)
        {
            DeclareWarButton.Disabled = true;
            DeclareWarButton.Text = Loc.GetString("comms-console-menu-declare-war-button-locked",
                ("time", left.ToString(@"hh\:mm\:ss", CultureInfo.CurrentCulture)));
            return;
        }

        DeclareWarButton.Disabled = false;
        DeclareWarButton.Text = Loc.GetString("comms-console-menu-declare-war-button");
    }
}

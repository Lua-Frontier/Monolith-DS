using System.Numerics;
using Content.Client._LuaM.UserInterface.Hud;
using Content.Client.Stylesheets;
using Robust.Shared.Timing;

namespace Content.Client.Lobby.UI;

public sealed partial class LobbyGui
{
    public void SetRoundTimer(string timer, string caption)
    {
        if (RoundTimer.Text != timer)
            RoundTimer.Text = timer;

        if (_caption == caption && !string.IsNullOrEmpty(StartTime.Text))
            return;

        _caption = caption;
        StartTime.Text = caption.ToUpperInvariant();
    }

    public void SetStationTime(string text)
    {
        if (StationTime.Text != text)
            StationTime.Text = text;
    }

    private string? _caption;

    private float _fitTimer;
    private float _fitWidth;

    private const float TerminalMinWidth = 660;
    private const float TerminalChrome = 70;
    private const float TerminalMaxShare = 0.42f;
    private const float RoundRowMin = 124;
    private const float RoundRowMax = 380;

    private void HideBannerDuplicates()
    {
        var rules = Loc.GetString("server-info-rules-button");
        var guide = Loc.GetString("server-info-guidebook-button");
        foreach (var row in LinkBanner.Children)
        {
            foreach (var child in row.Children)
            {
                if (child is Robust.Client.UserInterface.Controls.Button { Text: { } text } button && (text == rules || text == guide))
                    button.Visible = false;
            }
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        CallVoteButton.Visible = !CallVoteButton.Disabled;

        var anyLink = false;
        foreach (var row in LinkBanner.Children)
        {
            foreach (var child in row.Children)
                anyLink |= child.Visible;
        }
        LinkBanner.Visible = anyLink;
        _fitTimer -= args.DeltaSeconds;
        if (_fitTimer <= 0 || MathF.Abs(_fitWidth - Width) > 0.5f)
        {
            _fitTimer = 0.5f;
            _fitWidth = Width;
            FitTerminal();
        }

        UpdateDeployStatus();

        var join = !ReadyButton.ToggleMode;
        if (join == ReadyButton.HasStyleClass(LobbyHudStyles.Join))
            return;

        ReadyButton.RemoveStyleClass(join ? LobbyHudStyles.Ready : LobbyHudStyles.Join);
        ReadyButton.AddStyleClass(join ? LobbyHudStyles.Join : LobbyHudStyles.Ready);
    }

    private void FitTerminal()
    {
        if (!DefaultState.Visible || Width <= 0)
            return;

        var wanted = MathF.Min(TerminalMinWidth, MathF.Max(TerminalMinWidth * 0.8f, Width * TerminalMaxShare));
        if (MathF.Abs(RightSide.SetWidth - wanted) > 2)
            RightSide.SetWidth = wanted;

        ServerInfo.Measure(new Vector2(wanted - TerminalChrome, float.PositiveInfinity));
        var height = Math.Clamp(ServerInfo.DesiredSize.Y + 14, RoundRowMin, RoundRowMax);
        if (MathF.Abs(NewsBox.SetHeight - height) > 1)
        {
            NewsBox.SetHeight = height;
        }
    }

    private HudStatus? _deployStatus;

    private void UpdateDeployStatus()
    {
        HudStatus status;
        string text;
        if (!ReadyButton.ToggleMode)
        {
            status = HudStatus.Advisory;
            text = "lobby-hud-status-round";
        }
        else if (ReadyButton.Pressed)
        {
            status = HudStatus.Nominal;
            text = "lobby-hud-status-ready";
        }
        else
        {
            status = HudStatus.Warning;
            text = "lobby-hud-status-not-ready";
        }

        if (_deployStatus == status)
            return;

        _deployStatus = status;
        DeployHeader.SetStatus(status == HudStatus.Advisory ? null : status, Loc.GetString(text));
        foreach (var st in Enum.GetValues<HudStatus>())
        {
            DeployWindow.RemoveStyleClass(LobbyHudStyles.WindowFor(st));
        }
        DeployWindow.RemoveStyleClass(LobbyHudStyles.Window);
        DeployWindow.AddStyleClass(status == HudStatus.Advisory ? LobbyHudStyles.Window : LobbyHudStyles.WindowFor(status));
    }
}

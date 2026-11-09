using Content.Shared._LuaM.RoundIntro;

namespace Content.Client.GameTicking.Managers;

public sealed partial class ClientGameTicker
{
    private bool LuaMJoinGameAttempt()
    {
        var ev = new ClientJoinGameAttemptEvent();
        RaiseLocalEvent(ref ev);
        return ev.Handled;
    }
}

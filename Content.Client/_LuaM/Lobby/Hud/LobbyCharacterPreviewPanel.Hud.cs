using System.Linq;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

public sealed partial class LobbyCharacterPreviewPanel
{
    public void SetProfileInfo(HumanoidCharacterProfile profile)
    {
        NameLabel.Text = profile.Name;

        var job = profile.JobPriorities.FirstOrDefault(p => p.Value == JobPriority.High).Key;
        RoleLabel.Text = job != default && IoCManager.Resolve<IPrototypeManager>().TryIndex<JobPrototype>(job, out var jobProto)
            ? jobProto.LocalizedName
            : Loc.GetString("lobby-hud-no-role");
    }
}

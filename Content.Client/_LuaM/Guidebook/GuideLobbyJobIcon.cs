/* LuaCorp - AGPLv3. See AGPLv3.txt. */
using System.Numerics;
using Content.Shared.Roles;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Guidebook;

/// <summary>The actual lobby job badge, enlarged without making a separate image copy.</summary>
public sealed class GuideLobbyJobIcon : TextureRect
{
    public string JobId { get; }

    public GuideLobbyJobIcon(string jobId)
    {
        JobId = jobId;
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        var job = prototypes.Index<JobPrototype>(jobId);
        var badge = prototypes.Index(job.Icon);
        Texture = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>().Frame0(badge.Icon);
        TextureScale = new Vector2(5, 5);
        SetSize = new Vector2(40, 40);
        CanShrink = true;
        Stretch = StretchMode.KeepAspectCentered;
        VerticalAlignment = VAlignment.Top;
        ToolTip = job.LocalizedName;
    }
}

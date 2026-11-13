/* LuaCorp - AGPLv3. See AGPLv3.txt for details. */
using System.Numerics;
using Content.Shared._Mono.PersonalShield;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Guidebook;

/// <summary>The actual in-world field shader, without spawning a wearer or a shield entity.</summary>
public sealed class GuideShieldPreview : Control
{
    private readonly PersonalShieldVisuals _visuals;
    private ShaderInstance? _shader;
    public GuideShieldPreview(PersonalShieldVisuals visuals)
    {
        _visuals = visuals;
        SetSize = new Vector2(96, 96);
        HorizontalAlignment = HAlignment.Center;
    }
    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        _shader ??= IoCManager.Resolve<IPrototypeManager>().Index<ShaderPrototype>("PersonalShieldSkin").InstanceUnique();
        _shader.SetParameter("progress", 1f);
        _shader.SetParameter("skin_color", _visuals.Color);
        _shader.SetParameter("brightness", _visuals.Brightness);
        _shader.SetParameter("pixel_grid", _visuals.PixelGrid);
        _shader.SetParameter("hex_density", _visuals.HexDensity);
        _shader.SetParameter("form_origin", _visuals.FormOrigin);
        _shader.SetParameter("fill_level", _visuals.FillLevel);
        _shader.SetParameter("line_level", _visuals.LineLevel);
        _shader.SetParameter("rim_level", _visuals.RimLevel);
        _shader.SetParameter("core_fade", _visuals.CoreFade);
        _shader.SetParameter("shard_scale", _visuals.ShardScale);
        _shader.SetParameter("alpha_bands", _visuals.AlphaBands);
        _shader.SetParameter("breath_depth", _visuals.BreathDepth);
        _shader.SetParameter("damage_flare", 0f);
        handle.UseShader(_shader);
        handle.DrawTextureRect(Texture.White, UIBox2.FromDimensions(Vector2.Zero, PixelSize));
        handle.UseShader(null);
    }
    protected override void ExitedTree()
    {
        _shader?.Dispose();
        _shader = null;
        base.ExitedTree();
    }
}

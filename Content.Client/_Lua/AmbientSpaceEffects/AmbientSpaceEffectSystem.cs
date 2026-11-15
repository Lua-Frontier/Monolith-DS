// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._Lua.AmbientSpaceEffects;

public sealed partial class AmbientSpaceEffectSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private AmbientSpaceEffectOverlay _overlay = default!;
    private NebulaWeatherOverlay _weatherOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new AmbientSpaceEffectOverlay(EntityManager, _prototypes);
        _overlays.AddOverlay(_overlay);
        _weatherOverlay = new NebulaWeatherOverlay(EntityManager, _prototypes);
        _overlays.AddOverlay(_weatherOverlay);

        Subs.CVar(_cfg, LuaCVars.AmbientSpaceEffectsQuality, v => _overlay.Quality = v, true);
        Subs.CVar(_cfg, LuaCVars.AmbientSpaceLayerLower, v => _overlay.LowerEnabled = v, true);
        Subs.CVar(_cfg, LuaCVars.AmbientSpaceLayerMid, v => _overlay.MidEnabled = v, true);
        Subs.CVar(_cfg, LuaCVars.AmbientSpaceLayerUpper, v => _overlay.UpperEnabled = v, true);
        Subs.CVar(_cfg, LuaCVars.AmbientSpaceEffectsDensity, v => _overlay.DensityMul = Math.Clamp(v, 0f, 1.5f), true);

        SubscribeLocalEvent<AmbientSpaceFieldComponent, ComponentShutdown>(OnFieldShutdown);
    }

    private void OnFieldShutdown(Entity<AmbientSpaceFieldComponent> ent, ref ComponentShutdown args)
    {
        _overlay.ForgetField(ent);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlays.RemoveOverlay(_overlay);
        _overlays.RemoveOverlay(_weatherOverlay);
    }
}

using Content.Server._LuaM.Shuttles; // LuaM
using Content.Server.Shuttles.Components;
using Content.Shared.CCVar;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Robust.Server.GameObjects;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    private void InitializeIFF()
    {
        SubscribeLocalEvent<IFFConsoleComponent, AnchorStateChangedEvent>(OnIFFConsoleAnchor);
        SubscribeLocalEvent<IFFConsoleComponent, IFFShowIFFMessage>(OnIFFShow);
        SubscribeLocalEvent<IFFConsoleComponent, IFFShowVesselMessage>(OnIFFShowVessel);
        SubscribeLocalEvent<IFFConsoleComponent, BoundUIOpenedEvent>(OnIFFConsoleOpen);
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
    }

    private void OnGridSplit(ref GridSplitEvent ev)
    {
        var splitMass = _cfg.GetCVar(CCVars.HideSplitGridsUnder);

        if (splitMass < 0)
            return;

        foreach (var grid in ev.NewGrids)
        {
            if (!_physicsQuery.TryGetComponent(grid, out var physics) ||
                physics.Mass > splitMass)
            {
                continue;
            }

            AddIFFFlag(grid, IFFFlags.HideLabel);
        }
    }

    // LuaM-start: consoles that only work on the ships of certain shipyards
    private IFFFlags GetAllowedIFFFlags(IFFConsoleComponent component, EntityUid? grid)
    {
        if (component.AllowedShipGroups.Count == 0)
            return component.AllowedFlags;

        return TryComp<ShipyardOriginComponent>(grid, out var origin) && component.AllowedShipGroups.Contains(origin.Group)
            ? component.AllowedFlags
            : IFFFlags.None;
    }
    // LuaM-end

    private void OnIFFConsoleOpen(EntityUid uid, IFFConsoleComponent component, ref BoundUIOpenedEvent args)
    {
        // Make sure UI state is up-to-date when opening the UI
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null)
        {
            _uiSystem.SetUiState(uid, IFFConsoleUiKey.Key, new IFFConsoleBoundUserInterfaceState()
            {
                AllowedFlags = GetAllowedIFFFlags(component, xform?.GridUid), // LuaM
                Flags = IFFFlags.None,
            });
            return;
        }

        if (!TryComp<IFFComponent>(xform.GridUid, out var iff))
        {
            _uiSystem.SetUiState(uid, IFFConsoleUiKey.Key, new IFFConsoleBoundUserInterfaceState()
            {
                AllowedFlags = GetAllowedIFFFlags(component, xform?.GridUid), // LuaM
                Flags = IFFFlags.None,
            });
            return;
        }

        _uiSystem.SetUiState(uid, IFFConsoleUiKey.Key, new IFFConsoleBoundUserInterfaceState()
        {
            AllowedFlags = GetAllowedIFFFlags(component, xform.GridUid), // LuaM
            Flags = iff.Flags,
        });
    }

    private void OnIFFShow(EntityUid uid, IFFConsoleComponent component, IFFShowIFFMessage args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null)
            return;

        // LuaM-start: ship check, and the imperial console hides the detection label as well
        var allowed = GetAllowedIFFFlags(component, xform.GridUid);
        if ((allowed & IFFFlags.HideLabel) == 0x0)
            return;

        var flags = IFFFlags.HideLabel | (allowed & IFFFlags.HideLabelAlways);
        // LuaM-end

        if (!args.Show)
        {
            AddIFFFlag(xform.GridUid.Value, flags); // LuaM: HideLabel > flags
        }
        else
        {
            RemoveIFFFlag(xform.GridUid.Value, flags); // LuaM: HideLabel > flags
        }
    }

    private void OnIFFShowVessel(EntityUid uid, IFFConsoleComponent component, IFFShowVesselMessage args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null ||
            (GetAllowedIFFFlags(component, xform.GridUid) & IFFFlags.Hide) == 0x0) // LuaM: ship check
        {
            return;
        }

        if (!args.Show)
        {
            AddIFFFlag(xform.GridUid.Value, IFFFlags.Hide);
        }
        else
        {
            RemoveIFFFlag(xform.GridUid.Value, IFFFlags.Hide);
        }
    }

    private void OnIFFConsoleAnchor(EntityUid uid, IFFConsoleComponent component, ref AnchorStateChangedEvent args)
    {
        // If we anchor / re-anchor then make sure flags up to date.
        if (!args.Anchored ||
            !TryComp(uid, out TransformComponent? xform) ||
            !TryComp<IFFComponent>(xform.GridUid, out var iff))
        {
            _uiSystem.SetUiState(uid, IFFConsoleUiKey.Key, new IFFConsoleBoundUserInterfaceState()
            {
                AllowedFlags = GetAllowedIFFFlags(component, Transform(uid).GridUid), // LuaM
                Flags = IFFFlags.None,
            });
        }
        else
        {
            _uiSystem.SetUiState(uid, IFFConsoleUiKey.Key, new IFFConsoleBoundUserInterfaceState()
            {
                AllowedFlags = GetAllowedIFFFlags(component, xform.GridUid), // LuaM
                Flags = iff.Flags,
            });
        }
    }

    protected override void UpdateIFFInterfaces(EntityUid gridUid, IFFComponent component)
    {
        base.UpdateIFFInterfaces(gridUid, component);

        var query = AllEntityQuery<IFFConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            _uiSystem.SetUiState(uid, IFFConsoleUiKey.Key, new IFFConsoleBoundUserInterfaceState()
            {
                AllowedFlags = GetAllowedIFFFlags(comp, gridUid), // LuaM
                Flags = component.Flags,
            });
        }
    }
}

/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using Content.Shared._LuaM.Guidebook;
using Robust.Shared.Network;
using Content.Shared.GameTicking;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Guidebook;

public sealed class GuidebookCatalogClientSystem : EntitySystem
{
    [Dependency] private IClientNetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    private TimeSpan _nextRequest;
    private TimeSpan _pendingSince;
    public GuidebookCatalog? Catalog { get; private set; }
    public event Action? Updated;
    private bool _pending;

    public override void Initialize()
    {
        base.Initialize();
        _net.Disconnect += OnDisconnect;
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => Clear(request: true));
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => Clear(request: true));
        SubscribeNetworkEvent<GuidebookCatalogInvalidatedEvent>(_ => Clear(request: true));
        SubscribeNetworkEvent<GuidebookImagesInvalidatedEvent>(_ => Clear(request: true));
        SubscribeNetworkEvent<UpdateGuidebookCatalogEvent>(ev =>
        {
            Catalog = ev.Data;
            _pending = false;
            Updated?.Invoke();
        });
    }

    private void OnDisconnect(object? sender, NetDisconnectedArgs args)
    { Clear(); }

    private void Clear(bool request = false)
    {
        Catalog = null;
        _pending = false;
        _nextRequest = TimeSpan.Zero;
        Updated?.Invoke();
        if (request && Updated != null && _net.IsConnected) Request();
    }

    public void Request() => Request(force: false);

    public void Request(bool force)
    {
        if (_net.IsConnected && !_pending && _timing.CurTime >= _nextRequest && (force || Catalog == null))
        {
            _pending = true;
            _pendingSince = _timing.CurTime;
            _nextRequest = _timing.CurTime + TimeSpan.FromSeconds(1.1);
            RaiseNetworkEvent(new RequestGuidebookCatalogEvent());
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_pending && _timing.CurTime - _pendingSince > TimeSpan.FromSeconds(30))
        {
            _pending = false;
            if (Updated != null && Catalog == null) Request();
        }
    }

    public override void Shutdown()
    {
        _net.Disconnect -= OnDisconnect;
        Catalog = null;
        base.Shutdown();
    }
}

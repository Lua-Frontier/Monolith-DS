using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Camera;
using Content.Shared.Input;
using Content.Shared.Movement.Components;
using Content.Shared.Shuttles.Components;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client._Mono.Movement.Systems;

public sealed class FocusToggleSystem : EntitySystem
{
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;
    [Dependency] private IPlayerManager _player = default!;

    private const float MaxOffset = 3f;
    private const float EdgeOffset = 0.9f;
    private const float Sharpness = 8f;

    private EntityUid? _owner;
    private bool _active;
    private Vector2 _target;
    private Vector2 _current;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContentEyeComponent, GetEyeOffsetEvent>(OnGetEyeOffset);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ToggleFocus, InputCmdHandler.FromDelegate(OnToggleFocus))
            .Register<FocusToggleSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        CommandBinds.Unregister<FocusToggleSystem>();
    }

    private void OnToggleFocus(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { } uid || uid != _player.LocalEntity)
            return;

        if (TryComp<PilotComponent>(uid, out var pilot) && pilot.Console != null)
            return;

        _active = !_active;
    }

    private void OnGetEyeOffset(Entity<ContentEyeComponent> ent, ref GetEyeOffsetEvent args)
    {
        if (ent.Owner != _owner)
            return;

        args.Offset += _current;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var player = _player.LocalEntity;
        if (player != _owner)
        {
            _owner = player;
            _active = false;
            _target = Vector2.Zero;
            _current = Vector2.Zero;
        }

        if (player == null)
            return;

        if (_active && TryComp<PilotComponent>(player.Value, out var pilot) && pilot.Console != null)
            _active = false;

        if (!_active)
            _target = Vector2.Zero;
        else if (TryGetMouseOffset(out var offset))
            _target = offset;

        _current = Vector2.Lerp(_current, _target, 1f - MathF.Exp(-Sharpness * frameTime));
        if (!_active && _current.LengthSquared() < 0.0001f)
            _current = Vector2.Zero;
    }

    private bool TryGetMouseOffset(out Vector2 offset)
    {
        offset = Vector2.Zero;

        var mousePos = _inputManager.MouseScreenPosition;
        if (mousePos.Window == WindowId.Invalid)
            return false;

        if (_uiManager.ActiveScreen == null || !_uiManager.ActiveScreen.TryGetWidget<MainViewport>(out var mainViewport))
            return false;

        var screenSize = mainViewport.Size;
        var minValue = MathF.Min(screenSize.X / 2, screenSize.Y / 2) * EdgeOffset;
        if (minValue <= 0f)
            return false;

        var normalized = new Vector2(-(mousePos.X - screenSize.X / 2) / minValue, (mousePos.Y - screenSize.Y / 2) / minValue);
        var eyeRotation = _eyeManager.CurrentEye.Rotation;
        offset = Vector2.Transform(normalized, Quaternion.CreateFromAxisAngle(-Vector3.UnitZ, (float) eyeRotation.Opposite().Theta));

        offset *= MaxOffset;
        if (offset.Length() > MaxOffset)
            offset = offset.Normalized() * MaxOffset;

        return true;
    }
}

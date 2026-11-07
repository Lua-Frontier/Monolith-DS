using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using static Content.Client.Mapping.MappingState;

namespace Content.Client.Mapping;

public sealed partial class MappingOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    // 1 off in case something else uses these colors since we use them to compare
    private static readonly Color PickColor = new(1, 255, 0);
    private static readonly Color DeleteColor = new(255, 1, 0);

    private readonly Dictionary<EntityUid, Color> _oldColors = new();

    private readonly MappingState _state;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public MappingOverlay(MappingState state)
    {
        IoCManager.InjectDependencies(this);

        _state = state;
        _shader = _prototypes.Index<ShaderPrototype>(UnshadedShader).Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var (id, color) in _oldColors)
        {
            if (!_entities.TryGetComponent(id, out SpriteComponent? sprite))
                continue;

            if (sprite.Color == DeleteColor || sprite.Color == PickColor)
                _entities.System<SpriteSystem>().SetColor((id, sprite), color);
        }

        _oldColors.Clear();

        if (_player.LocalEntity == null)
            return;

        var handle = args.WorldHandle;
        handle.UseShader(_shader);

        switch (_state.State)
        {
            case CursorState.Pick:
            {
                if (_state.GetHoveredEntity() is { } entity &&
                    _entities.TryGetComponent(entity, out SpriteComponent? sprite))
                {
                    _oldColors[entity] = sprite.Color;
                    _entities.System<SpriteSystem>().SetColor((entity, sprite), PickColor);
                }

                break;
            }
            case CursorState.Delete:
            {
                if (_state.GetHoveredEntity() is { } entity &&
                    _entities.TryGetComponent(entity, out SpriteComponent? sprite))
                {
                    _oldColors[entity] = sprite.Color;
                    _entities.System<SpriteSystem>().SetColor((entity, sprite), DeleteColor);
                }

                break;
            }
        }

        handle.UseShader(null);
    }
}

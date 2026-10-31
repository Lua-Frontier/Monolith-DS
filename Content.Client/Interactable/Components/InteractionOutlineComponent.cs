using Content.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Interactable.Components
{
    [RegisterComponent]
    public sealed partial class InteractionOutlineComponent : Component
    {
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private IEntityManager _entMan = default!;

        private const float DefaultWidth = 1;

        private static readonly ProtoId<ShaderPrototype> ShaderInRange = "SelectionOutlineInrange";

        private static readonly ProtoId<ShaderPrototype> ShaderOutOfRange = "SelectionOutline";

        private bool _inRange;
        private ShaderInstance? _shader;
        private int _lastRenderScale;

        // LuaM: per-id post shaders since RT 286, the legacy single PostShader wiped the other effects
        private SpriteSystem Sprite => _entMan.System<SpriteSystem>();

        public void OnMouseEnter(EntityUid uid, bool inInteractionRange, int renderScale)
        {
            _lastRenderScale = renderScale;
            _inRange = inInteractionRange;
            if (_entMan.TryGetComponent(uid, out SpriteComponent? sprite)
                && !Sprite.HasPostShader((uid, sprite), ContentPostShaderIds.InteractionOutline)) // LuaM
            {
                // TODO why is this creating a new instance of the outline shader every time the mouse enters???
                _shader = MakeNewShader(inInteractionRange, renderScale);
                SetOutline((uid, sprite), _shader); // LuaM
            }
        }

        public void OnMouseLeave(EntityUid uid)
        {
            if (_entMan.TryGetComponent(uid, out SpriteComponent? sprite))
            {
                Sprite.RemovePostShader((uid, sprite), ContentPostShaderIds.InteractionOutline); // LuaM
                sprite.RenderOrder = 0;
            }

            _shader?.Dispose();
            _shader = null;
        }

        public void UpdateInRange(EntityUid uid, bool inInteractionRange, int renderScale)
        {
            if (_entMan.TryGetComponent(uid, out SpriteComponent? sprite)
                && Sprite.HasPostShader((uid, sprite), ContentPostShaderIds.InteractionOutline) // LuaM
                && (inInteractionRange != _inRange || _lastRenderScale != renderScale))
            {
                _inRange = inInteractionRange;
                _lastRenderScale = renderScale;

                _shader = MakeNewShader(_inRange, _lastRenderScale);
                SetOutline((uid, sprite), _shader); // LuaM
            }
        }

        // LuaM start
        private void SetOutline(Entity<SpriteComponent?> sprite, ShaderInstance shader)
        {
            Sprite.SetPostShader(sprite, new SpriteComponent.PostShaderArgs(ContentPostShaderIds.InteractionOutline, shader)
            {
                After = ContentPostShaderIds.AfterBaseEffects,
            });
        }
        // LuaM end

        private ShaderInstance MakeNewShader(bool inRange, int renderScale)
        {
            var shaderName = inRange ? ShaderInRange : ShaderOutOfRange;

            var instance = _prototypeManager.Index<ShaderPrototype>(shaderName).InstanceUnique();
            instance.SetParameter("outline_width", DefaultWidth * renderScale);
            return instance;
        }
    }
}

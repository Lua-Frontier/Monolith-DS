// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Numerics;
using System.Linq;
using Content.Shared._LuaM.Announcements;
using Content.Client.Humanoid;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Holopad;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Map;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.Announcements;

/// <summary>
/// Reconstructs appearance-only, client-local dolls from announcement snapshots. The speaker need
/// not be in PVS. Like the lobby dress-up doll, these entities never enter a real map.
/// Hologram rendering uses the existing holopad shader and its parameter contract.
/// </summary>
public sealed partial class AnnouncementPortraitView : Control
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IGameTiming _timing = default!;
    private static readonly EntProtoId HolopadPrototype = "HolopadHologram";
    // A fixed anatomical crop: fit the upper sprite section to the viewport, never fit the full body.
    public const float UpperBodyFraction = 0.58f;

    private AnnouncementPortraitSnapshot? _snapshot;
    private EntityUid? _doll;
    private readonly List<EntityUid> _items = new();
    private IRenderTexture? _target;
    private ShaderInstance? _shader;
    private bool _captured;
    private bool _unavailable;
    private float _stripes;
    private float _scrollRate;

    public Color Accent { get; set; } = Color.FromHex("#65B8E2");

    /// <summary>Temporary local doll, released after capture or when the view leaves the screen.</summary>
    public EntityUid? PreviewEntity => _doll;

    public AnnouncementPortraitView()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
        SetSize = new Vector2(72, 86);
        VerticalAlignment = VAlignment.Top;
    }

    public void SetSnapshot(AnnouncementPortraitSnapshot? snapshot)
    {
        Release();
        _snapshot = snapshot;
        if (IsInsideTree && VisibleInTree)
            Reconstruct();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        if (VisibleInTree)
            Reconstruct();
    }

    protected override void ExitedTree()
    {
        Release();
        base.ExitedTree();
    }

    protected override void VisibilityChanged(bool newVisible)
    {
        base.VisibilityChanged(newVisible);
        if (IsInsideTree && VisibleInTree)
            Reconstruct();
        else
            Release();
    }

    private void Reconstruct()
    {
        if (_snapshot is not { } snapshot || _doll != null || _captured || _unavailable)
            return;
        var prototype = snapshot.Prototype;
        if (snapshot.Species is { } species)
        {
            if (!_prototypes.TryIndex<SpeciesPrototype>(species, out var speciesPrototype))
                return;
            prototype = speciesPrototype.DollPrototype;
        }
        if (!_prototypes.HasIndex<EntityPrototype>(prototype))
            return;

        try
        {
            var doll = _entities.SpawnEntity(prototype, MapCoordinates.Nullspace);
            _doll = doll;
            if (snapshot.Species != null && _entities.TryGetComponent<HumanoidAppearanceComponent>(doll, out var humanoid))
            {
                humanoid.Species = snapshot.Species;
                humanoid.Sex = snapshot.Sex;
                humanoid.Gender = snapshot.Gender;
                humanoid.Age = snapshot.Age;
                humanoid.SkinColor = snapshot.SkinColor;
                humanoid.EyeColor = snapshot.EyeColor;
                humanoid.Height = snapshot.Height;
                humanoid.Width = snapshot.Width;
                humanoid.MarkingSet = new MarkingSet(snapshot.Markings);
                humanoid.CustomBaseLayers = new(snapshot.CustomBaseLayers);
                humanoid.PermanentlyHidden = new(snapshot.PermanentlyHidden);
                humanoid.HiddenLayers.Clear();
                // Rebuild through the same native appearance path as profile loading. Keep the
                // doll's previous marking list so its default marking layers are removed too.
                _entities.System<HumanoidAppearanceSystem>().RefreshAnnouncementAppearance((doll, humanoid));
                if (_entities.TryGetComponent<SpriteComponent>(doll, out var humanoidSprite))
                {
                    var width = float.IsFinite(snapshot.Width) ? Math.Clamp(snapshot.Width, 0.1f, 4) : 1;
                    var height = float.IsFinite(snapshot.Height) ? Math.Clamp(snapshot.Height, 0.1f, 4) : 1;
                    _entities.System<SpriteSystem>().SetScale((doll, humanoidSprite), new Vector2(width, height));
                }
            }

            var inventory = _entities.System<InventorySystem>();
            var clothing = _entities.System<ClothingSystem>();
            foreach (var equipped in snapshot.Equipment)
            {
                if (!_prototypes.HasIndex<EntityPrototype>(equipped.Prototype))
                    continue;
                var item = _entities.SpawnEntity(equipped.Prototype, MapCoordinates.Nullspace);
                _items.Add(item);
                if (_entities.TryGetComponent<ClothingComponent>(item, out var component))
                    clothing.SetEquippedPrefix(item, equipped.EquippedPrefix, component);
                if (!inventory.TryEquip(doll, item, equipped.Slot, silent: true, force: true))
                {
                    _entities.DeleteEntity(item);
                    _items.Remove(item);
                }
            }
            if (_entities.TryGetComponent<SpriteComponent>(doll, out var sprite))
                _entities.System<AppearanceSystem>().OnChangeData(doll, sprite);
        }
        catch
        {
            // Partially reconstructed equipment must never survive a failed local preview.
            Release();
            throw;
        }
    }

    protected override void Draw(IRenderHandle render)
    {
        base.Draw(render);
        var handle = render.DrawingHandleScreen;
        if (PixelWidth <= 0 || PixelHeight <= 0)
            return;
        if (_target != null && _target.Size != PixelSize)
        {
            _target.Dispose();
            _target = null;
            _captured = false;
        }
        if (!_captured && !Capture(render))
            return;
        if (_shader == null)
        {
            // Use the exact stock holopad presentation instead of a separate approximation.
            var prototype = _prototypes.Index(HolopadPrototype);
            if (!prototype.TryComp(out HolopadHologramComponent? hologram, _entities.ComponentFactory))
                return;
            _shader = _prototypes.Index<ShaderPrototype>(hologram.ShaderName).InstanceUnique();
            _shader.SetParameter("color1", new Vector3(Accent.R, Accent.G, Accent.B));
            _shader.SetParameter("color2", new Vector3(Accent.R, Accent.G, Accent.B) * 0.57f);
            _shader.SetParameter("alpha", hologram.Alpha);
            _shader.SetParameter("intensity", hologram.Intensity);
            _scrollRate = hologram.ScrollRate;
            _shader.SetParameter("texHeight", _stripes);
        }
        _shader.SetParameter("t", (float) _timing.CurTime.TotalSeconds * _scrollRate);
        handle.UseShader(_shader);
        handle.DrawTextureRect(_target!.Texture, PixelSizeBox);
        handle.UseShader(null);
    }

    private bool Capture(IRenderHandle render)
    {
        Reconstruct();
        if (_doll is not { } doll || !_entities.TryGetComponent<SpriteComponent>(doll, out var sprite))
        {
            _unavailable = true;
            ReleaseEntities();
            return false;
        }
        var handle = render.DrawingHandleScreen;
        try
        {
            var sprites = _entities.System<SpriteSystem>();
            sprites.ForceUpdate(doll);
            // Match holopad UI rendering: preserve clothing displacement without an off-screen
            // stencil test that otherwise cuts clothing out of some humanoid species.
            var layerCount = sprite.AllLayers.Count();
            for (var i = 0; i < layerCount; i++)
            {
                if (!sprites.TryGetLayer((doll, sprite), i, out var layer, false))
                    continue;
                var shader = layer.ShaderPrototype == "DisplacedStencilDraw" || layer.ShaderPrototype == "HologramDisplacedDraw"
                    ? "HologramDisplacedDraw" : "unshaded";
                if (layer.ShaderPrototype != shader)
                    sprite.LayerSetShader(i, shader);
            }
            var bounds = sprites.GetLocalBounds((doll, sprite));
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                _unavailable = true;
                return false;
            }
            if (_target == null || _target.Size != PixelSize)
            {
                _target?.Dispose();
                _target = _clyde.CreateRenderTarget(PixelSize,
                    new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb, true),
                    new TextureSampleParameters { Filter = false }, nameof(AnnouncementPortraitView));
            }
            // The upper part fills the viewport. A width-based fit would shrink the full body back
            // into the window and show the legs. The viewport clips shoulders/gear at its sides.
            var ppm = EyeManager.PixelsPerMeter;
            var zoom = Math.Max(1, PixelHeight - 4 * UIScale) / (bounds.Height * ppm * UpperBodyFraction);
            var center = bounds.Center + sprite.Offset;
            var position = new Vector2(PixelWidth / 2f - center.X * ppm * zoom,
                2 * UIScale + (bounds.Top + sprite.Offset.Y) * ppm * zoom);
            handle.RenderInRenderTarget(_target, () =>
            {
                render.SetScissor(null);
                handle.SetTransform(Matrix3x2.Identity);
                handle.DrawEntity(doll, position, new Vector2(zoom), Angle.Zero, Angle.Zero, Direction.South, sprite);
            }, Color.Transparent);
            _stripes = PixelHeight / MathF.Max(zoom, 1);
            _shader?.SetParameter("texHeight", _stripes);
            _captured = true;
            return true;
        }
        catch
        {
            Release();
            throw;
        }
        finally
        {
            ReleaseEntities();
        }
    }

    private void ReleaseEntities()
    {
        // Track all created items, including items not parented by a successful inventory equip.
        foreach (var item in _items)
            if (_entities.EntityExists(item))
                _entities.DeleteEntity(item);
        _items.Clear();
        if (_doll is { } doll && _entities.EntityExists(doll))
            _entities.DeleteEntity(doll);
        _doll = null;
    }

    private void Release()
    {
        ReleaseEntities();
        _captured = false;
        _unavailable = false;
        _shader?.Dispose();
        _shader = null;
        _target?.Dispose();
        _target = null;
    }
}

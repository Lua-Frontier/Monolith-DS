#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Content.Client._LuaM.Announcements;
using Content.Shared._LuaM.Announcements;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class AnnouncementPortraitViewTests
{
    [Test]
    public async Task StaticPortraitCapturesOnceAndResizeCapturesAgainWithoutKeepingEntities()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
            using var portrait = new AnnouncementPortraitView();
            var render = new PortraitRenderHandle(Texture.White);
            var draw = typeof(AnnouncementPortraitView).GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(IRenderHandle) }, null)!;
            try
            {
                portrait.SetSnapshot(new AnnouncementPortraitSnapshot { Prototype = "MobHuman", Species = "Human" });
                ui.WindowRoot.AddChild(portrait);
                portrait.Measure(new Vector2(72, 86));
                portrait.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(72, 86)));
                var doll = portrait.PreviewEntity!.Value;
                var sprite = entities.GetComponent<SpriteComponent>(doll);
                var bounds = entities.System<SpriteSystem>().GetLocalBounds((doll, sprite));
                draw.Invoke(portrait, new object[] { render });
                Assert.That(render.EntityDraws, Is.EqualTo(1));
                Assert.That(render.TextureDraws, Is.EqualTo(1),
                    "Only the stock hologram pass is composited; no original-colour portrait is drawn over it.");
                var fullHeight = render.LastScale.Y * bounds.Height * EyeManager.PixelsPerMeter;
                Assert.That(fullHeight, Is.GreaterThan(portrait.PixelHeight),
                    "The legs must be outside the viewport, not fitted into a full-body portrait.");
                Assert.That(fullHeight * AnnouncementPortraitView.UpperBodyFraction,
                    Is.EqualTo(portrait.PixelHeight - 4 * portrait.UIScale).Within(0.01f));
                Assert.That(portrait.PreviewEntity, Is.Null);
                Assert.That(entities.EntityExists(doll), Is.False);
                for (var i = 0; i < 30; i++)
                    draw.Invoke(portrait, new object[] { render });
                Assert.That(render.EntityDraws, Is.EqualTo(1), "Unchanged frames must only composite the cached texture.");
                portrait.SetSize = new Vector2(96, 110);
                portrait.Measure(new Vector2(96, 110));
                portrait.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(96, 110)));
                draw.Invoke(portrait, new object[] { render });
                Assert.That(render.EntityDraws, Is.EqualTo(2), "A changed target size needs exactly one new capture.");
                Assert.That(portrait.PreviewEntity, Is.Null);
                portrait.Visible = false;
                portrait.Visible = true;
                Assert.That(portrait.PreviewEntity, Is.Not.Null, "Showing again must rebuild after releasing the old cache.");
            }
            finally
            {
                portrait.Orphan();
                portrait.SetSnapshot(null);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(null, null, true)]
    [TestCase("head", "ClothingHeadHatBeret", true)]
    [TestCase("outerClothing", "ClothingOuterHardsuitEngineering", true)]
    [TestCase("head", "ClothingHeadHelmetHardsuitEngineering", false)]
    public async Task RestoredHairHasRealVisibleSpriteLayersAfterEquipment(string? slot, string? equipment, bool visible)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
            var sprites = entities.System<SpriteSystem>();
            var color = Color.FromHex("#AC3867");
            var snapshot = new AnnouncementPortraitSnapshot { Prototype = "MobHuman", Species = "Human" };
            snapshot.Markings.AddBack(MarkingCategories.Hair, new Marking("HumanHairAfro", new[] { color }));
            if (slot != null && equipment != null)
                snapshot.Equipment.Add(new AnnouncementPortraitItem { Slot = slot, Prototype = equipment });
            using var portrait = new AnnouncementPortraitView();
            try
            {
                portrait.SetSnapshot(snapshot);
                ui.WindowRoot.AddChild(portrait);
                var doll = portrait.PreviewEntity!.Value;
                var sprite = entities.GetComponent<SpriteComponent>(doll);
                // Assert the actual RSI marking layer, rather than only copied Humanoid fields.
                Assert.That(sprites.LayerMapTryGet((doll, sprite), "HumanHairAfro-afro", out var index, false), Is.True);
                Assert.That(sprite[index].Visible, Is.EqualTo(visible));
                Assert.That(sprite[index].RsiState.Name, Is.EqualTo("afro"));
                if (visible)
                    Assert.That(sprite[index].Color, Is.EqualTo(color));
                if (slot != null)
                    Assert.That(entities.System<InventorySystem>().TryGetSlotEntity(doll, slot, out _), Is.True);
                Assert.That(snapshot.Markings.Markings[MarkingCategories.Hair][0].Visible, Is.True,
                    "Clothing occlusion on the doll must never mutate the received snapshot.");
            }
            finally
            {
                portrait.Orphan();
                portrait.SetSnapshot(null);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SnapshotDollIsLazyClonedEquippedAndReleasedOnEveryLifecyclePath()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
            var inventory = entities.System<InventorySystem>();
            var snapshot = new AnnouncementPortraitSnapshot
            {
                Prototype = "MobHuman",
                Species = "Human",
                SkinColor = Color.FromHex("#825A40"),
                EyeColor = Color.FromHex("#40AC86"),
                Age = 49,
                Height = 0.93f,
                Width = 1.08f,
                Equipment = { new AnnouncementPortraitItem { Slot = "head", Prototype = "ClothingHeadHatBeret", EquippedPrefix = "" } },
            };
            var portrait = new AnnouncementPortraitView();
            try
            {
                portrait.SetSnapshot(snapshot);
                Assert.That(portrait.PreviewEntity, Is.Null, "Queued/inactive previews must not spawn entities.");
                portrait.Visible = false;
                ui.WindowRoot.AddChild(portrait);
                Assert.That(portrait.PreviewEntity, Is.Null, "A hidden editor preview must stay lazy.");
                portrait.Visible = true;
                var doll = portrait.PreviewEntity!.Value;
                var appearance = entities.GetComponent<HumanoidAppearanceComponent>(doll);
                Assert.That(appearance.Species.Id, Is.EqualTo("Human"));
                Assert.That(appearance.Age, Is.EqualTo(49));
                Assert.That(appearance.Height, Is.EqualTo(0.93f));
                Assert.That(appearance.SkinColor, Is.EqualTo(snapshot.SkinColor));
                Assert.That(appearance.EyeColor, Is.EqualTo(snapshot.EyeColor));
                Assert.That(appearance.MarkingSet, Is.Not.SameAs(snapshot.Markings));
                Assert.That(appearance.CustomBaseLayers, Is.Not.SameAs(snapshot.CustomBaseLayers));
                Assert.That(appearance.PermanentlyHidden, Is.Not.SameAs(snapshot.PermanentlyHidden));
                Assert.That(inventory.TryGetSlotEntity(doll, "head", out var head), Is.True);
                var hat = head!.Value;
                Assert.That(entities.GetComponent<ClothingComponent>(hat).EquippedPrefix, Is.EqualTo(""));

                portrait.Visible = false;
                Assert.That(portrait.PreviewEntity, Is.Null);
                Assert.That(entities.EntityExists(doll), Is.False);
                Assert.That(entities.EntityExists(hat), Is.False);
                portrait.Visible = true;
                doll = portrait.PreviewEntity!.Value;
                portrait.SetSnapshot(new AnnouncementPortraitSnapshot { Prototype = "MobMouse" });
                Assert.That(entities.EntityExists(doll), Is.False, "Replacing the snapshot must release the old doll.");
                var mouse = portrait.PreviewEntity!.Value;
                Assert.That(entities.HasComponent<HumanoidAppearanceComponent>(mouse), Is.False);
                portrait.SetSnapshot(null);
                Assert.That(portrait.PreviewEntity, Is.Null);
                Assert.That(entities.EntityExists(mouse), Is.False);

                portrait.SetSnapshot(snapshot);
                doll = portrait.PreviewEntity!.Value;
                Assert.That(inventory.TryGetSlotEntity(doll, "head", out head), Is.True);
                hat = head!.Value;
                portrait.Orphan();
                Assert.That(portrait.PreviewEntity, Is.Null);
                Assert.That(entities.EntityExists(doll), Is.False);
                Assert.That(entities.EntityExists(hat), Is.False);
            }
            finally
            {
                portrait.Orphan();
                portrait.SetSnapshot(null);
            }
        });
        await pair.CleanReturnAsync();
    }
    // Records the expensive entity submission while exercising the real cache/lifecycle path.
    // It deliberately does not claim to verify GPU pixels or hologram shader appearance.
    private sealed class PortraitRenderHandle(Texture white) : IRenderHandle
    {
        public int EntityDraws => ((PortraitScreen) DrawingHandleScreen).EntityDraws;
        public int TextureDraws => ((PortraitScreen) DrawingHandleScreen).TextureDraws;
        public Vector2 LastScale => ((PortraitScreen) DrawingHandleScreen).LastScale;
        public DrawingHandleScreen DrawingHandleScreen { get; } = new PortraitScreen(white);
        public DrawingHandleWorld DrawingHandleWorld => throw new NotSupportedException();
        public void SetScissor(UIBox2i? box) { }
        public void RenderInRenderTarget(IRenderTarget target, Action action, Color? clearColor) => action();
        public void DrawEntity(EntityUid entity, Vector2 position, Vector2 scale, Angle? worldRot,
            Angle eyeRotation = default, Direction? overrideDirection = null, SpriteComponent? sprite = null,
            TransformComponent? xform = null, SharedTransformSystem? xformSystem = null,
            IReadOnlyList<SpriteComponent.PostShaderEntry>? postShaders = null) => ((PortraitScreen) DrawingHandleScreen).EntityDraws++;
    }

    private sealed class PortraitScreen(Texture white) : DrawingHandleScreen(white)
    {
        public int EntityDraws;
        public int TextureDraws;
        public Vector2 LastScale;
        public override void DrawRect(UIBox2 rect, Color color, bool filled = true) { }
        public override void DrawTextureRectRegion(Texture texture, UIBox2 rect, UIBox2? subRegion = null, Color? modulate = null) => TextureDraws++;
        public override void SetTransform(in Matrix3x2 matrix) { }
        public override Matrix3x2 GetTransform() => Matrix3x2.Identity;
        public override void UseShader(ShaderInstance? shader) { }
        public override ShaderInstance? GetShader() => null;
        public override void DrawCircle(Vector2 position, float radius, Color color, bool filled = true) { }
        public override void DrawLine(Vector2 from, Vector2 to, Color color) { }
        public override void RenderInRenderTarget(IRenderTarget target, Action action, Color? clearColor) => action();
        public override void DrawTexture(Texture texture, Vector2 position, Color? modulate = null) { }
        public override void DrawPrimitives(DrawPrimitiveTopology topology, Texture texture, ReadOnlySpan<DrawVertexUV2DColor> vertices) { }
        public override void DrawPrimitives(DrawPrimitiveTopology topology, Texture texture, ReadOnlySpan<ushort> indices,
            ReadOnlySpan<DrawVertexUV2DColor> vertices) { }
        public override void DrawEntity(EntityUid entity, Vector2 position, Vector2 scale, Angle? worldRot,
            Angle eyeRotation = default, Direction? overrideDirection = null, SpriteComponent? sprite = null,
            TransformComponent? xform = null, SharedTransformSystem? xformSystem = null)
        {
            EntityDraws++;
            LastScale = scale;
        }
    }
}

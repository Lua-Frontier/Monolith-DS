#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Content.Client._LuaM.Announcements;
using Content.Shared._LuaM.Announcements;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class AnnouncementCardDrawingTests
{
    [Test]
    public async Task AnnouncementFrameDrawsDuringConstrainedLayout()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            using var card = new ScreenAnnouncementCard();
            card.SetAnnouncement(new ScreenAnnouncementEvent("System", "Received announcement"));
            var draw = typeof(ScreenAnnouncementCard).GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(DrawingHandleScreen) }, null)!;
            using var handle = new TestDrawingHandle(Texture.White);
            foreach (var size in new[] { new Vector2(1, 1), new Vector2(5, 5), new Vector2(80, 80),
                         new Vector2(304, 103), new Vector2(320, 216), new Vector2(640, 216) })
            {
                card.Measure(size);
                card.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                handle.Rects.Clear();
                draw.Invoke(card, new object[] { handle });
                Assert.That(handle.Rects, Is.Not.Empty, $"Frame must draw at {size}.");
                foreach (var rect in handle.Rects)
                    Assert.That(rect.Width >= 0 && rect.Height >= 0, Is.True);
            }
        });
        await pair.CleanReturnAsync();
    }
        private sealed class TestDrawingHandle(Texture white) : DrawingHandleScreen(white)
        {
            public List<UIBox2> Rects { get; } = new();

            public override void SetTransform(in Matrix3x2 matrix)
            {
            }

            public override Matrix3x2 GetTransform() => Matrix3x2.Identity;

            public override void UseShader(ShaderInstance? shader)
            {
            }

            public override ShaderInstance? GetShader() => null;

            public override void DrawPrimitives(DrawPrimitiveTopology primitiveTopology, Texture texture,
                ReadOnlySpan<DrawVertexUV2DColor> vertices)
            {
            }

            public override void DrawPrimitives(DrawPrimitiveTopology primitiveTopology, Texture texture,
                ReadOnlySpan<ushort> indices, ReadOnlySpan<DrawVertexUV2DColor> vertices)
            {
            }

            public override void DrawCircle(Vector2 position, float radius, Color color, bool filled = true)
            {
            }

            public override void DrawLine(Vector2 from, Vector2 to, Color color)
            {
            }

            public override void RenderInRenderTarget(IRenderTarget target, Action a, Color? clearColor)
            {
            }

            public override void DrawTexture(Texture texture, Vector2 position, Color? modulate = null)
            {
            }

            public override void DrawRect(UIBox2 rect, Color color, bool filled = true)
            {
                Rects.Add(rect);
            }

            public override void DrawTextureRectRegion(Texture texture, UIBox2 rect, UIBox2? subRegion = null,
                Color? modulate = null)
            {
                Rects.Add(rect);
            }

            public override void DrawEntity(EntityUid entity, Vector2 position, Vector2 scale, Angle? worldRot,
                Angle eyeRotation = default, Direction? overrideDirection = null, SpriteComponent? sprite = null,
                TransformComponent? xform = null, SharedTransformSystem? xformSystem = null)
            {
            }
        }
}

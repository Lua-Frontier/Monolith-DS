using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class HudVignette : Control
{
    private const int Columns = 24;
    private const int Rows = 14;

    public float BaseDim { get; set; } = 0.22f;
    public float EdgeDim { get; set; } = 0.78f;
    public float Inner { get; set; } = 0.35f;
    public float Outer { get; set; } = 1.15f;

    private readonly DrawVertexUV2DColor[] _vertices = new DrawVertexUV2DColor[Columns * Rows * 6];

    public HudVignette()
    {
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalExpand = true;
        VerticalExpand = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var w = PixelSize.X;
        var h = PixelSize.Y;
        var n = 0;
        for (var y = 0; y < Rows; y++)
        {
            for (var x = 0; x < Columns; x++)
            {
                Add(ref n, x, y, w, h);
                Add(ref n, x + 1, y, w, h);
                Add(ref n, x + 1, y + 1, w, h);
                Add(ref n, x, y, w, h);
                Add(ref n, x + 1, y + 1, w, h);
                Add(ref n, x, y + 1, w, h);
            }
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _vertices);
    }

    private void Add(ref int n, int gx, int gy, float w, float h)
    {
        var u = (float) gx / Columns;
        var v = (float) gy / Rows;
        var dx = (u - 0.5f) * 2f;
        var dy = (v - 0.5f) * 2f;
        var r = MathF.Sqrt(dx * dx + dy * dy) / MathF.Sqrt(2f) * 1.25f;
        var t = Math.Clamp((r - Inner) / (Outer - Inner), 0f, 1f);
        t = t * t * (3f - 2f * t);
        var alpha = Math.Clamp(BaseDim + (EdgeDim - BaseDim) * t, 0f, 1f);

        _vertices[n++] = new DrawVertexUV2DColor(new Vector2(u * w, v * h), Vector2.Zero, new Color(0f, 0f, 0f, alpha));
    }
}

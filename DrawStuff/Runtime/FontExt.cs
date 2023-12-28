
namespace DrawStuff;

using System.Drawing;
using System.Numerics;

public static class FontExt {

    public static RectangleF AddText(this Geometry<SpriteVertex> b, Vector2 pos, BakedFont font, string text) {
        var (tw, th) = ((float)font.Texture.Width, (float)font.Texture.Height);
        var startPos = pos;
        var (maxX, maxY) = (pos.X, pos.Y);
        var info = font.Info;
        foreach (char c in text) {
            if (info.charMap.TryGetValue(c, out int i)) {
                var bounds = info.GlyphBounds[i];
                var cropping = info.Cropping[i];
                var offset = pos + new Vector2(cropping.X, cropping.Y);
                b.AddQuad(
                    offset.X,
                    offset.Y,
                    bounds.Width,
                    bounds.Height,
                    bounds.X / tw, bounds.Y / th, bounds.Width / tw, bounds.Height / th,
                    (xPos, yPos, xTex, yTex) =>
                        new SpriteVertex(new(xPos, yPos), new(xTex, yTex), Colour.White.RGBA));
                var kerning = info.Kerning[i].Z;
                pos += new Vector2(bounds.Width + kerning, 0);
                maxX = MathF.Max(maxX, offset.X + bounds.Width);
                maxY = MathF.Max(maxY, offset.Y + bounds.Height);
            }
        }
        return new(startPos.X, startPos.Y, maxX - startPos.X, maxY - startPos.Y);
    }

    public static BakedFont LoadDefaultFont(this IDrawStuff ds, int pixelHeight = 32) {
        return Font.Load(ds, pixelHeight, BundledData.GetFile("Fonts/Roboto-Black.ttf"));
    }
}

public struct GlyphInfo {
    public int X, Y, Width, Height;
    public int XOffset, YOffset;
    public int XAdvance;
}

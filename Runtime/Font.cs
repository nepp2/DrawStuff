
using Silk.NET.OpenGL;
using StbTrueTypeSharp;
using System.Runtime.InteropServices;
using Silk.NET.Maths;

namespace DrawStuff;

public record struct TextRaster(byte[] Data, int Width, int Height);

public record BakedFont(
    GLTexture Texture,
    List<Rectangle<int>> GlyphBounds,
    List<Rectangle<int>> Cropping,
    List<char> Chars,
    int LineSpacing,
    int Spacing,
    List<Vector3D<float>> Kerning,
    char DefaultChar)
{
    public Dictionary<char, int> charMap =
        Chars.Select((c, i) => (c, i)).ToDictionary(tup => tup.c, tup => tup.i);
}

public class Font {
    private const int FontBitmapWidth = 1024;
    private const int FontBitmapHeight = 1024;

    public static void DrawText(SpriteBuffer sb, Vector2D<float> pos, BakedFont font, string text) {
        var (tw, th) = ((float)font.Texture.Width, (float)font.Texture.Height);
        foreach (char c in text) {
            if (font.charMap.TryGetValue(c, out int i)) {
                var bounds = font.GlyphBounds[i].As<float>();
                var offset = pos + font.Cropping[i].Origin.As<float>();
                sb.PushQuad(
                    offset.X, offset.Y, bounds.Size.X, bounds.Size.Y,
                    bounds.Origin.X / tw, bounds.Origin.Y / th, bounds.Size.X / tw, bounds.Size.Y / th,
                    Colour.White);
                var kerning = font.Kerning[i].Z;
                pos += new Vector2D<float>(bounds.Size.X + kerning, 0);
            }
        }
    }

    public static BakedFont Load(GL gl, string fontPath) {
        var bytes = File.ReadAllBytes(fontPath);
        return Load(gl, bytes);
    }

    public static BakedFont Load(GL gl, byte[] fontBytes) {
        var fontBaker = new FontBaker();

        fontBaker.Begin(FontBitmapWidth, FontBitmapHeight);
        fontBaker.Add(fontBytes, 32, new[]
        {
            CharacterRange.BasicLatin,
            CharacterRange.Latin1Supplement,
            CharacterRange.LatinExtendedA,
            CharacterRange.Cyrillic,
            CharacterRange.Greek
        });

        var charData = fontBaker.End();

        // Offset by minimal offset
        var minimumOffsetY = 10000;
        foreach (var pair in charData.Glyphs)
            if (pair.Value.YOffset < minimumOffsetY)
                minimumOffsetY = pair.Value.YOffset;

        var keys = charData.Glyphs.Keys.ToArray();
        foreach (var key in keys) {
            var pc = charData.Glyphs[key];
            pc.YOffset -= minimumOffsetY;
            charData.Glyphs[key] = pc;
        }

        var rgb = new Colour[FontBitmapWidth * FontBitmapHeight];
        for (var i = 0; i < charData.Bitmap.Length; ++i) {
            var b = charData.Bitmap[i];
            rgb[i] = new(b, b, b, b);
        }

        var fontTexture = GLTexture.Create(gl, MemoryMarshal.Cast<Colour, byte>(rgb), FontBitmapWidth, FontBitmapHeight);

        var glyphBounds = new List<Rectangle<int>>();
        var cropping = new List<Rectangle<int>>();
        var chars = new List<char>();
        var kerning = new List<Vector3D<float>>();

        var orderedKeys = charData.Glyphs.Keys.OrderBy(a => a);
        foreach (var key in orderedKeys) {
            var character = charData.Glyphs[key];

            var bounds = new Rectangle<int>(character.X, character.Y,
                character.Width,
                character.Height);

            glyphBounds.Add(bounds);
            cropping.Add(new (character.XOffset, character.YOffset, bounds.Size.X, bounds.Size.Y));

            chars.Add((char)key);

            kerning.Add(new (0, bounds.Size.X, character.XAdvance - bounds.Size.X));
        }

        return new (
            fontTexture, glyphBounds, cropping,
            chars, 20, 0, kerning, ' ');
    }

    public static class Builtins {
        public static BakedFont LoadRoboto(GL gl) {
            return Load(gl, BundledData.GetFile("Fonts/Roboto-Black.ttf"));
        }
    }
}

public struct GlyphInfo {
    public int X, Y, Width, Height;
    public int XOffset, YOffset;
    public int XAdvance;
}

public struct CharacterRange {
    public static readonly CharacterRange BasicLatin = new CharacterRange(0x0020, 0x007F);
    public static readonly CharacterRange Latin1Supplement = new CharacterRange(0x00A0, 0x00FF);
    public static readonly CharacterRange LatinExtendedA = new CharacterRange(0x0100, 0x017F);
    public static readonly CharacterRange LatinExtendedB = new CharacterRange(0x0180, 0x024F);
    public static readonly CharacterRange Cyrillic = new CharacterRange(0x0400, 0x04FF);
    public static readonly CharacterRange CyrillicSupplement = new CharacterRange(0x0500, 0x052F);
    public static readonly CharacterRange Hiragana = new CharacterRange(0x3040, 0x309F);
    public static readonly CharacterRange Katakana = new CharacterRange(0x30A0, 0x30FF);
    public static readonly CharacterRange Greek = new CharacterRange(0x0370, 0x03FF);
    public static readonly CharacterRange CjkSymbolsAndPunctuation = new CharacterRange(0x3000, 0x303F);
    public static readonly CharacterRange CjkUnifiedIdeographs = new CharacterRange(0x4e00, 0x9fff);
    public static readonly CharacterRange HangulCompatibilityJamo = new CharacterRange(0x3130, 0x318f);
    public static readonly CharacterRange HangulSyllables = new CharacterRange(0xac00, 0xd7af);

    public int Start { get; }

    public int End { get; }

    public int Size => End - Start + 1;

    public CharacterRange(int start, int end) {
        Start = start;
        End = end;
    }

    public CharacterRange(int single) : this(single, single) {
    }
}

public unsafe class FontBaker {
    private byte[]? _bitmap;
    private StbTrueType.stbtt_pack_context _context = new();
    private Dictionary<int, GlyphInfo> _glyphs = new();
    private int bitmapWidth, bitmapHeight;

    public void Begin(int width, int height) {
        bitmapWidth = width;
        bitmapHeight = height;
        _bitmap = new byte[width * height];
        _context = new StbTrueType.stbtt_pack_context();

        fixed (byte* pixelsPtr = _bitmap) {
            StbTrueType.stbtt_PackBegin(_context, pixelsPtr, width, height, width, 1, null);
        }

        _glyphs = new Dictionary<int, GlyphInfo>();
    }

    public void Add(byte[] ttf, float fontPixelHeight,
        IEnumerable<CharacterRange> characterRanges) {
        if (ttf == null || ttf.Length == 0)
            throw new ArgumentNullException(nameof(ttf));

        if (fontPixelHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(fontPixelHeight));

        if (characterRanges == null)
            throw new ArgumentNullException(nameof(characterRanges));

        if (!characterRanges.Any())
            throw new ArgumentException("characterRanges must have a least one value.");

        var fontInfo = StbTrueType.CreateFont(ttf, 0);
        if (fontInfo == null)
            throw new Exception("Failed to init font.");

        var scaleFactor = StbTrueType.stbtt_ScaleForPixelHeight(fontInfo, fontPixelHeight);

        int ascent, descent, lineGap;
        StbTrueType.stbtt_GetFontVMetrics(fontInfo, &ascent, &descent, &lineGap);

        foreach (var range in characterRanges) {
            if (range.Start > range.End)
                continue;

            var cd = new StbTrueType.stbtt_packedchar[range.End - range.Start + 1];
            fixed (StbTrueType.stbtt_packedchar* chardataPtr = cd) {
                StbTrueType.stbtt_PackFontRange(_context, fontInfo.data, 0, fontPixelHeight,
                    range.Start,
                    range.End - range.Start + 1,
                    chardataPtr);
            }

            for (var i = 0; i < cd.Length; ++i) {
                var yOff = cd[i].yoff;
                yOff += ascent * scaleFactor;

                var glyphInfo = new GlyphInfo {
                    X = cd[i].x0,
                    Y = cd[i].y0,
                    Width = cd[i].x1 - cd[i].x0,
                    Height = cd[i].y1 - cd[i].y0,
                    XOffset = (int)cd[i].xoff,
                    YOffset = (int)Math.Round(yOff),
                    XAdvance = (int)Math.Round(cd[i].xadvance)
                };

                _glyphs[i + range.Start] = glyphInfo;
            }
        }
    }

    public FontBakerResult End() {
        return new FontBakerResult(_glyphs, _bitmap!, bitmapWidth, bitmapHeight);
    }
}

public record FontBakerResult(Dictionary<int, GlyphInfo> Glyphs, byte[] Bitmap, int Width, int Height);


using StbImageSharp;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DrawStuff;

public interface GPUTexture {
    int Width { get; }
    int Height { get; }
}

[StructLayout(LayoutKind.Sequential)]
public struct PixelRGBA {
    public byte R;
    public byte G;
    public byte B;
    public byte A;
}

public record struct Subtexture<T>(TextureBase<T> Src, int X, int Y, int W, int H) where T : struct {
    public static implicit operator TCQuad(Subtexture<T> t) =>
        TCQuad.FromRect(
            (float)t.X / t.Src.Width, (float)t.Y / t.Src.Height,
            (float)t.W / t.Src.Width, (float)t.H / t.Src.Height);
}

public record TCQuad(Vector2 A, Vector2 B, Vector2 C, Vector2 D) {
    public static TCQuad FromRect(float x, float y, float w, float h) =>
        new(new(x, y), new(x + w, y), new(x + w, y + h), new(x, y + h));
}

public interface TextureBase<T> where T : struct {
    byte[] Data { get; }
    int Width { get; }
    int Height { get; }
    Span<T> Pixels { get; }
    Span<T> Row(int row);
}

public class TextureImpl<T> : TextureBase<T> where T : struct {

    public byte[] Data { get; }
    public int Width { get; }
    public int Height { get; }

    public Span<T> Pixels => MemoryMarshal.Cast<byte, T>(Data);

    public Span<T> Row(int row) => Pixels[(row * Width)..][0..Width];

    public static implicit operator Subtexture<T>(TextureImpl<T> t) => new(t, 0, 0, t.Width, t.Height);

    public Subtexture<T> GetSubtexture(int x, int y, int w, int h) {
        Debug.Assert(x + w <= Width && y + h <= Height);
        return new(this, x, y, w, h);
    }

    public Subtexture<T> Blit(int x, int y, Subtexture<T> tex) {
        Debug.Assert(x + tex.W <= Width && y + tex.H <= Height);
        for(int i = 0; i < tex.H; ++i) {
            var src = tex.Src.Row(tex.Y + i)[tex.X..][0..tex.W];
            var dest = Row(y + i)[x..];
            src.CopyTo(dest);
        }
        return GetSubtexture(x, y, tex.W, tex.H);
    }

    public TextureImpl(byte[] data, int width, int height) {
        Data = data;
        Width = width;
        Height = height;
        Debug.Assert(data.Length == width * height * Marshal.SizeOf<T>());
    }
}

public class Texture : TextureImpl<PixelRGBA> {
    public Texture(byte[] data, int width, int height) : base(data, width, height) {}

    public Texture(int width, int height)
        : base(new byte[width * height * Marshal.SizeOf<PixelRGBA>()], width, height) {}

    public static Texture Load(string path) {
        var image = ImageResult.FromMemory(
            File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
        return new(image.Data, image.Width, image.Height);
    }
}

public class TextureMono : TextureImpl<byte> {
    public TextureMono(byte[] data, int width, int height) : base(data, width, height) { }

    public TextureMono(int width, int height)
        : base(new byte[width * height], width, height) { }
}
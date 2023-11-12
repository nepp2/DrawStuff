
using StbImageSharp;

namespace DrawStuff;

public interface GPUTexture {
    int Width { get; }
    int Height { get; }
}

public record Texture(byte[] Data, int Width, int Height) {
    public static Texture Load(string path) {
        var image = ImageResult.FromMemory(
            File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
        return new(image.Data, image.Width, image.Height);
    }
}
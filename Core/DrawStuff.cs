using Silk.NET.Windowing;
using System.Numerics;

namespace DrawStuff;

public interface IDrawStuff : IDisposable {

    IWindow Window { get; }

    public static IDrawStuff StartDrawing(IWindow window) => new OpenGL.GLDrawStuff(window);

    Shader<Vertex, Vars> LoadShader<Vertex, Vars>(ShaderConfig<Vertex, Vars> config)
        where Vertex : unmanaged;

    GPUGeometry<Vertex, Triangle> CreateGPUGeometry<Vertex>(ShaderConfig config)
        where Vertex : unmanaged;

    // Create a camera that uses pixel coordinates with the origin in the top left
    Matrix4x4 GetPixelCamera() =>
        Matrix4x4.CreateScale(2f / Window.Size.X, -2f / Window.Size.Y, 1f)
            * Matrix4x4.CreateTranslation(-1f, 1f, 0f);

    void ClearWindow();
    void ClearDepth();

    Texture LoadTexture(string path) => Texture.Load(path);

    GPUTexture LoadGPUTexture(ReadOnlySpan<byte> bytes, int width, int height);
    GPUTexture LoadGPUTexture(Texture tex) => LoadGPUTexture(tex.Data, tex.Width, tex.Height);
    GPUTexture LoadGPUTexture(string path) => LoadGPUTexture(LoadTexture(path));
}

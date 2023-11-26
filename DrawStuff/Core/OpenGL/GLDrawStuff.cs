using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Drawing;

namespace DrawStuff.OpenGL;

public class GLDrawStuff : IDrawStuff {

    public IWindow Window { get; }

    private GL? _gl;

    public GL GetGL() {
        if (_gl == null)
            _gl = GL.GetApi(Window);
        return _gl;
    }

    public GLDrawStuff(IWindow window) {
        Window = window;
    }

    public void Dispose() {
        Window.Dispose();
    }

    public Shader<Vertex, Vars> LoadShader<Vertex, Vars>(ShaderConfig<Vertex, Vars> config)
        where Vertex : unmanaged
            => new GLShader<Vertex, Vars>(this, config);

    public GPUGeometry<Vertex, Triangle> CreateGPUGeometry<Vertex>(ShaderConfig config)
        where Vertex : unmanaged
            => new GLGeometry<Vertex, Triangle>(this, config.VertexAttribs);

    public void ClearWindow(Color c) {
        var gl = GetGL();
        gl.ClearColor(c);
        gl.Clear((uint)ClearBufferMask.ColorBufferBit | (uint)ClearBufferMask.DepthBufferBit);
    }

    public void ClearDepth() {
        GetGL().Clear((uint)ClearBufferMask.DepthBufferBit);
    }

    public GPUTexture LoadGPUTexture(ReadOnlySpan<byte> bytes, int width, int height) =>
        GLTexture.Create(GetGL(), bytes, width, height);
}

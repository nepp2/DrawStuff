using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using System.Runtime.CompilerServices;
using static DrawStuff.ShaderLanguage;

namespace DrawStuff;

public interface ShapeArray<Vertex, ShapeType> : IDisposable
    where Vertex : unmanaged
    where ShapeType : unmanaged
{
    ShapeBuilder<Vertex, ShapeType> CreateBuilder() {
        return new();
    }

    void OverwriteAll(in ShapeBuilder<Vertex, ShapeType> builder);
}


public class ShapeArrayGL<Vertex, ShapeType> : ShapeArray<Vertex, ShapeType>
    where Vertex : unmanaged
    where ShapeType : unmanaged 
{
    public GLVertexArray<Vertex, ShapeType> VertexArray { get; }

    public ShapeArrayGL(DrawStuffGL draw, GLAttribute[] vertexAttribs) {
        var gl = draw.GetGL();
        var vbo = new GLBufferObject<Vertex>(gl, BufferTargetARB.ArrayBuffer);
        var ebo = new GLBufferObject<ShapeType>(gl, BufferTargetARB.ElementArrayBuffer);
        VertexArray = new GLVertexArray<Vertex, ShapeType>(gl, vbo, ebo, vertexAttribs);
    }

    public void Dispose() {
        VertexArray.Dispose();
    }

    // Overwrites all existing shapes with the full contents of the builder
    public void OverwriteAll(in ShapeBuilder<Vertex, ShapeType> builder) {
        VertexArray.Vbo.UpdateBuffer(builder.Verts);
        VertexArray.Ebo.UpdateBuffer(builder.Shapes);
    }
}

public class ShapeBuilder<Vertex, ShapeType>
    where Vertex : unmanaged where ShapeType : unmanaged
{
    public ValueBuffer<Vertex> Verts { get; } = new();
    public ValueBuffer<ShapeType> Shapes { get; } = new();

    public int VertexCount => Verts.Count;

    public void Clear() {
        Verts.Clear();
        Shapes.Clear();
    }

    public void PushVert(in Vertex v) => Verts.Push(v);
    public void PushIndices(in ShapeType shape) => Shapes.Push(shape);
}

public static class ShapeBuilderExt {
    public static void PushTriangle<Vertex>(
        this ShapeBuilder<Vertex, Triangle> b,
        in Vertex v1, in Vertex v2, in Vertex v3
    )
    where Vertex : unmanaged
    {
        uint i = (uint)b.VertexCount;
        b.PushVert(v1);
        b.PushVert(v2);
        b.PushVert(v3);
        b.PushIndices(new(i, i + 1, i + 2));
    }

    public static void PushQuad<Vertex>(
        this ShapeBuilder<Vertex, Triangle> b,
        in Vertex v1, in Vertex v2, in Vertex v3, in Vertex v4
    )
    where Vertex : unmanaged
    {
        uint i = (uint)b.VertexCount;
        b.PushVert(v3);
        b.PushVert(v4);
        b.PushVert(v2);
        b.PushVert(v1);
        b.PushIndices(new(i + 0, i + 1, i + 2));
        b.PushIndices(new(i + 1, i + 2, i + 3));
    }

    public delegate Vert ToVertex<Vert>(float xPos, float yPos, float xTex, float yTex);

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static void PushQuad<Vertex>(
        this ShapeBuilder<Vertex, Triangle> b,
        float x, float y, float w, float h,
        float tx, float ty, float tw, float th,
        ToVertex<Vertex> toVert)
            where Vertex : unmanaged
    {
        uint i = (uint)b.VertexCount;
        var (x2, y2) = (x + w, y + h);
        var (tx2, ty2) = (tx + tw, ty + th);
        b.PushVert(toVert(x2, y2, tx2, ty2));
        b.PushVert(toVert(x2, y, tx2, ty));
        b.PushVert(toVert(x, y, tx, ty));
        b.PushVert(toVert(x, y2, tx, ty2));
        b.PushIndices(new(i + 0, i + 1, i + 3));
        b.PushIndices(new(i + 1, i + 2, i + 3));
    }
}

public interface IDrawStuff : IDisposable {

    IWindow Window { get; }

    public static IDrawStuff StartDrawing(IWindow window) => new DrawStuffGL(window);

    Shader<Vertex, Vars> LoadShader<Vertex, Vars>(ShaderConfig<Vertex, Vars> config)
        where Vertex : unmanaged;

    ShapeArray<Vertex, ShapeType> CreateShapeArray<Vertex, ShapeType>(ShaderConfig config)
        where Vertex : unmanaged
        where ShapeType : unmanaged;

    // Create a camera that uses pixel coordinates with the origin in the top left
    Matrix4X4<float> GetPixelCamera() =>
        Matrix4X4.CreateScale(2f / Window.Size.X, -2f / Window.Size.Y, 1f)
            * Matrix4X4.CreateTranslation(-1f, 1f, 0f);

    void ClearWindow();

    BakedFont LoadDefaultFont();
}

public class DrawStuffGL : IDrawStuff {

    public IWindow Window { get; }

    private GL? _gl;

    public GL GetGL() {
        if(_gl == null)
            _gl = GL.GetApi(Window);
        return _gl;
    }

    public DrawStuffGL(IWindow window) {
        Window = window;
    }

    public void Dispose() {
        Window.Dispose();
    }

    public Shader<Vertex, Vars> LoadShader<Vertex, Vars>(ShaderConfig<Vertex, Vars> config)
        where Vertex : unmanaged
            => new Shader<Vertex, Vars>(this, config);

    public ShapeArray<Vertex, ShapeType> CreateShapeArray<Vertex, ShapeType>(ShaderConfig config)
        where Vertex : unmanaged
        where ShapeType : unmanaged
            => new ShapeArrayGL<Vertex, ShapeType>(this, config.VertexAttribs);

    public void ClearWindow() {
        GetGL().Clear((uint)ClearBufferMask.ColorBufferBit | (uint)ClearBufferMask.DepthBufferBit);
    }

    public BakedFont LoadDefaultFont() =>
        Font.Builtins.LoadRoboto(GetGL());
}

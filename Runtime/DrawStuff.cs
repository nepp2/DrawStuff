using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Numerics;
using System.Runtime.CompilerServices;
using static DrawStuff.ShaderLanguage;

namespace DrawStuff;

public interface GPUGeometry<Vertex, Shape> : IDisposable
    where Vertex : unmanaged     
    where Shape : unmanaged
{
    void OverwriteAll(in Geometry<Vertex> builder);
}


public class GPUGeometryGL<Vertex, Shape> : GPUGeometry<Vertex, Shape>
    where Vertex : unmanaged 
    where Shape : unmanaged
{
    public GLVertexArray<Vertex, Triangle> VertexArray { get; }

    public GPUGeometryGL(DrawStuffGL draw, GLAttribute[] vertexAttribs) {
        var gl = draw.GetGL();
        var vbo = new GLBufferObject<Vertex>(gl, BufferTargetARB.ArrayBuffer);
        var ebo = new GLBufferObject<Triangle>(gl, BufferTargetARB.ElementArrayBuffer);
        VertexArray = new GLVertexArray<Vertex, Triangle>(gl, vbo, ebo, vertexAttribs);
    }

    public void Dispose() {
        VertexArray.Dispose();
    }

    // Overwrites all existing shapes with the full contents of the builder
    public void OverwriteAll(in Geometry<Vertex> b) {
        VertexArray.Vbo.UpdateBuffer(b.Verts);
        VertexArray.Ebo.UpdateBuffer(b.Triangles);
    }
}

public class Geometry<Vertex>
    where Vertex : unmanaged
{
    public ValueBuffer<Vertex> Verts { get; } = new();
    public ValueBuffer<Triangle> Triangles { get; } = new();

    public int VertexCount => Verts.Count;

    public void Clear() {
        Verts.Clear();
        Triangles.Clear();
    }

    public Geometry<Vertex> PushVert(in Vertex v) {
        Verts.Push(v);
        return this;
    }

    public Geometry<Vertex> PushShape(in Triangle shape) {
        Triangles.Push(shape);
        return this;
    }
}

public static class GeometryExt {

    public static void PushVert(
        this Geometry<Vec3> b,
        float x, float y, float z
    ) {
        b.PushVert(new (x, y ,z));
    }

    public static void PushTriangle<Vertex>(
        this Geometry<Vertex> b,
        in Vertex v1, in Vertex v2, in Vertex v3
    )
    where Vertex : unmanaged
    {
        uint i = (uint)b.VertexCount;
        b.PushVert(v1);
        b.PushVert(v2);
        b.PushVert(v3);
        b.PushShape(new(i, i + 1, i + 2));
    }

    public static void PushQuad<Vertex>(
        this Geometry<Vertex> b,
        in Vertex v1, in Vertex v2, in Vertex v3, in Vertex v4
    )
    where Vertex : unmanaged
    {
        uint i = (uint)b.VertexCount;
        b.PushVert(v3);
        b.PushVert(v4);
        b.PushVert(v2);
        b.PushVert(v1);
        b.PushShape(new(i + 0, i + 1, i + 2));
        b.PushShape(new(i + 1, i + 3, i + 2));
    }

    public delegate Vert ToVertex<Vert>(float xPos, float yPos, float xTex, float yTex);

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static void PushQuad<Vertex>(
        this Geometry<Vertex> b,
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
        b.PushShape(new(i + 0, i + 1, i + 3));
        b.PushShape(new(i + 1, i + 2, i + 3));
    }

    public static void PushFrom<Vertex>(
        this Geometry<Vertex> b,
        Geometry<Vertex> other)
            where Vertex : unmanaged 
    {
        uint offset = (uint)b.Verts.Count;
        foreach (var v in other.Verts.AsReadOnlySpan())
            b.Verts.Push(v);
        foreach (var t in other.Triangles.AsReadOnlySpan())
            b.Triangles.Push(new(offset + t.A, offset + t.B, offset + t.C));
    }

    public static void PushMap<Vertex, NewVertex>(
    this Geometry<Vertex> b,
    Geometry<NewVertex> other,
    Func<NewVertex, Vertex> vertexMap)
        where Vertex : unmanaged
        where NewVertex : unmanaged
    {
        uint offset = (uint)b.Verts.Count;
        foreach (var v in other.Verts.AsReadOnlySpan())
            b.Verts.Push(vertexMap(v));
        foreach (var t in other.Triangles.AsReadOnlySpan())
            b.Triangles.Push(new(offset + t.A, offset + t.B, offset + t.C));
    }
}

public interface IDrawStuff : IDisposable {

    IWindow Window { get; }

    public static IDrawStuff StartDrawing(IWindow window) => new DrawStuffGL(window);

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

    BakedFont LoadDefaultFont(int size = 32);
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

    public GPUGeometry<Vertex, Triangle> CreateGPUGeometry<Vertex>(ShaderConfig config)
        where Vertex : unmanaged
            => new GPUGeometryGL<Vertex, Triangle>(this, config.VertexAttribs);

    public void ClearWindow() {
        GetGL().Clear((uint)ClearBufferMask.ColorBufferBit | (uint)ClearBufferMask.DepthBufferBit);
    }

    public void ClearDepth() {
        GetGL().Clear((uint)ClearBufferMask.DepthBufferBit);
    }

    public BakedFont LoadDefaultFont(int size) =>
        Font.Builtins.LoadRoboto(GetGL(), size);
}

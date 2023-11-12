using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static DrawStuff.ShaderLanguage;

namespace DrawStuff;

[StructLayout(LayoutKind.Sequential)]
public record struct Triangle(uint A, uint B, uint C);

public interface GPUGeometry<Vertex, Shape> : IDisposable
    where Vertex : unmanaged
    where Shape : unmanaged {
    void OverwriteAll(in Geometry<Vertex> builder);
}

public class Geometry<Vertex>
    where Vertex : unmanaged {
    public ValueBuffer<Vertex> Verts { get; } = new();
    public ValueBuffer<Triangle> Triangles { get; } = new();

    public int VertexCount => Verts.Count;

    public void Clear() {
        Verts.Clear();
        Triangles.Clear();
    }

    public Geometry<Vertex> AddVert(in Vertex v) {
        Verts.Push(v);
        return this;
    }

    public Geometry<Vertex> AddTriangle(in Triangle shape) {
        Triangles.Push(shape);
        return this;
    }

    public Geometry<Vertex> AddTriangle(uint a, uint b, uint c) {
        Triangles.Push(new(a, b, c));
        return this;
    }
}

public static class GeometryExt {

    public static void AddVert(
        this Geometry<Vec3> b,
        float x, float y, float z
    ) {
        b.AddVert(new(x, y, z));
    }

    public static void AddTriangle<Vertex>(
        this Geometry<Vertex> b,
        in Vertex v1, in Vertex v2, in Vertex v3
    )
    where Vertex : unmanaged {
        uint i = (uint)b.VertexCount;
        b.AddVert(v1);
        b.AddVert(v2);
        b.AddVert(v3);
        b.AddTriangle(new(i, i + 1, i + 2));
    }

    public static void AddQuad<Vertex>(
        this Geometry<Vertex> b,
        in Vertex v1, in Vertex v2, in Vertex v3, in Vertex v4
    )
    where Vertex : unmanaged {
        uint i = (uint)b.VertexCount;
        b.AddVert(v3);
        b.AddVert(v4);
        b.AddVert(v2);
        b.AddVert(v1);
        b.AddTriangle(new(i + 0, i + 1, i + 2));
        b.AddTriangle(new(i + 1, i + 3, i + 2));
    }

    public delegate Vert ToVertex<Vert>(float xPos, float yPos, float xTex, float yTex);

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static void AddQuad<Vertex>(
        this Geometry<Vertex> b,
        float x, float y, float w, float h,
        float tx, float ty, float tw, float th,
        ToVertex<Vertex> toVert)
            where Vertex : unmanaged {
        uint i = (uint)b.VertexCount;
        var (x2, y2) = (x + w, y + h);
        var (tx2, ty2) = (tx + tw, ty + th);
        b.AddVert(toVert(x2, y2, tx2, ty2));
        b.AddVert(toVert(x2, y, tx2, ty));
        b.AddVert(toVert(x, y, tx, ty));
        b.AddVert(toVert(x, y2, tx, ty2));
        b.AddTriangle(new(i + 0, i + 1, i + 3));
        b.AddTriangle(new(i + 1, i + 2, i + 3));
    }

    public static void Append<Vertex>(
        this Geometry<Vertex> b,
        Geometry<Vertex> other)
            where Vertex : unmanaged {
        uint offset = (uint)b.Verts.Count;
        foreach (var v in other.Verts.AsReadOnlySpan())
            b.Verts.Push(v);
        foreach (var t in other.Triangles.AsReadOnlySpan())
            b.Triangles.Push(new(offset + t.A, offset + t.B, offset + t.C));
    }

    public static void Append<Vertex, NewVertex>(
    this Geometry<Vertex> b,
    Geometry<NewVertex> other,
    Func<NewVertex, Vertex> vertexMap)
        where Vertex : unmanaged
        where NewVertex : unmanaged {
        uint offset = (uint)b.Verts.Count;
        foreach (var v in other.Verts.AsReadOnlySpan())
            b.Verts.Push(vertexMap(v));
        foreach (var t in other.Triangles.AsReadOnlySpan())
            b.Triangles.Push(new(offset + t.A, offset + t.B, offset + t.C));
    }
}

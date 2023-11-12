using Silk.NET.OpenGL;

namespace DrawStuff.OpenGL;

public class GLGeometry<Vertex, Shape> : GPUGeometry<Vertex, Shape>
    where Vertex : unmanaged
    where Shape : unmanaged
{
    public GLVertexArray<Vertex, Triangle> VertexArray { get; }

    public GLGeometry(GLDrawStuff draw, GLAttribute[] vertexAttribs) {
        var gl = draw.GetGL();
        var vbo = new GLBufferObject<Vertex>(gl, BufferTargetARB.ArrayBuffer);
        var ebo = new GLBufferObject<Triangle>(gl, BufferTargetARB.ElementArrayBuffer);
        VertexArray = new GLVertexArray<Vertex, Triangle>(gl, vbo, ebo, vertexAttribs);
    }

    public void Dispose() {
        VertexArray.Vbo.Dispose();
        VertexArray.Ebo.Dispose();
        VertexArray.Dispose();
    }

    // Overwrites all existing shapes with the full contents of the builder
    public void OverwriteAll(in Geometry<Vertex> b) {
        VertexArray.Vbo.UpdateBuffer(b.Verts);
        VertexArray.Ebo.UpdateBuffer(b.Triangles);
    }
}

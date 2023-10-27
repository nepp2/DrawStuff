
using Silk.NET.OpenGL;

namespace DrawStuff;

public record struct RenderConfig<Vertex, Vars>(
    string VertexSrc,
    string FragmentSrc,
    RenderPipeline<Vertex, Vars>.SetShaderVars SetVars,
    GLAttribute[] VertexAttribs)
        where Vertex : unmanaged where Vars : unmanaged;

public class RenderPipeline {
    public static RenderPipeline<Vertex, Vars> Create<Vertex, Vars>(GL gl, RenderConfig<Vertex, Vars> config)
        where Vertex : unmanaged where Vars : unmanaged =>
            new (gl, config);
}

public class RenderPipeline<Vertex, Vars>
    where Vertex : unmanaged where Vars : unmanaged
{
    public delegate void SetShaderVars(GLShader shader, in Vars v);

    private GLVertexArray<Vertex, TriangleIndices> gpuBuffers;
    private GLShader shader;
    private SetShaderVars setVars;

    public RenderPipeline(GL gl, RenderConfig<Vertex, Vars> config) {
        var vbo = new GLBufferObject<Vertex>(gl, BufferTargetARB.ArrayBuffer);
        var ebo = new GLBufferObject<TriangleIndices>(gl, BufferTargetARB.ElementArrayBuffer);
        gpuBuffers = new GLVertexArray<Vertex, TriangleIndices>(gl, vbo, ebo, config.VertexAttribs);
        shader = GLShader.Compile(gl, config.VertexSrc, config.FragmentSrc);
        setVars = config.SetVars;
    }

    public void SetIndices(ReadOnlySpan<TriangleIndices> triangles) {
        gpuBuffers.Ebo.UpdateBuffer(triangles);
    }

    public void SetVertexData(ReadOnlySpan<Vertex> vertexData) {
        gpuBuffers.Vbo.UpdateBuffer(vertexData);
    }

    public ValueBuffer<TriangleIndices> CreateIndexBuffer() => new ValueBuffer<TriangleIndices>();
    public ValueBuffer<Vertex> CreateVertexBuffer() => new ValueBuffer<Vertex>();

    public void Render(in Vars vars) {
        shader.Bind();
        setVars(shader, vars);
        gpuBuffers.Draw(gpuBuffers.Ebo.Count * 3);
    }

}

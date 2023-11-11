
using Silk.NET.OpenGL;
using System.Runtime.InteropServices;

namespace DrawStuff;

public class Shader<Vertex, Vars>
    where Vertex : unmanaged
{
    private DrawStuffGL draw;
    private GLShader shader;
    private ShaderConfig<Vertex, Vars> config;
    private int[] uniformLocations;

    public Shader(DrawStuffGL draw, ShaderConfig<Vertex, Vars> config) {
        this.draw = draw;
        this.config = config;
        var gl = draw.GetGL();
        shader = GLShader.Compile(gl, config.VertexSrc, config.FragmentSrc);
        uniformLocations = config.Vars.Select(shader.GetUniformLocation).ToArray();
    }

    public GPUGeometry<Vertex, Triangle> CreateGPUGeometry()
            => new GPUGeometryGL<Vertex, Triangle>(draw, config.VertexAttribs);

    public Geometry<Vertex> CreateGeometry()
            => new();

    public GPUGeometry<Vertex, Triangle> LoadGeometry(Geometry<Vertex> builder) {
        var shapes = CreateGPUGeometry();
        shapes.OverwriteAll(builder);
        return shapes;
    }

    public void Draw<ShapeType>(in GPUGeometry<Vertex, ShapeType> shapes, in Vars vars)
        where ShapeType : unmanaged 
    {
        var gl = draw.GetGL();
        gl.Enable(EnableCap.CullFace);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthRange(-100000, 100000);
        shader.Bind();
        config.SetVars(shader, uniformLocations, vars);
        var glShapes = (GPUGeometryGL<Vertex, ShapeType>)shapes;
        var vertexArray = glShapes.VertexArray;
        int indicesPerShape = Marshal.SizeOf<ShapeType>() / sizeof(uint);
        vertexArray.Draw(vertexArray.Ebo.Count * indicesPerShape);
    }

}

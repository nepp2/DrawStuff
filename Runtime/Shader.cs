
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

    public ShapeArray<Vertex, ShapeType> CreateShapeArray<ShapeType>()
        where ShapeType : unmanaged
            => draw.CreateShapeArray<Vertex, ShapeType>(config);

    public ShapeArray<Vertex, Triangle> CreateTriangleArray()
        => CreateShapeArray<Triangle>();

    public ShapeBuilder<Vertex, ShapeType> CreateShapeBuilder<ShapeType>()
        where ShapeType : unmanaged
            => new();

    public ShapeBuilder<Vertex, Triangle> CreateTriangleBuilder()
        => new ();

    public ShapeArray<Vertex, ShapeType> LoadShapeArray<ShapeType>(ShapeBuilder<Vertex, ShapeType> builder)
    where ShapeType : unmanaged {
        var shapes = CreateShapeArray<ShapeType>();
        shapes.OverwriteAll(builder);
        return shapes;
    }

    public ShapeArray<Vertex, Triangle> CreateTriangleArray(ShapeBuilder<Vertex, Triangle> builder)
        => LoadShapeArray(builder);

    public void Draw<ShapeType>(in ShapeArray<Vertex, ShapeType> shapes, in Vars vars)
        where ShapeType : unmanaged 
    {
        shader.Bind();
        config.SetVars(shader, uniformLocations, vars);
        var glShapes = (ShapeArrayGL<Vertex, ShapeType>)shapes;
        var vertexArray = glShapes.VertexArray;
        int indicesPerShape = Marshal.SizeOf<ShapeType>() / sizeof(uint);
        vertexArray.Draw(vertexArray.Ebo.Count * indicesPerShape);
    }

}

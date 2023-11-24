namespace DrawStuff;

public interface Shader<Vertex, Vars> : IDisposable
    where Vertex : unmanaged {

    GPUGeometry<Vertex, Triangle> CreateGPUGeometry();

    Geometry<Vertex> CreateGeometry() => new();

    GPUGeometry<Vertex, Triangle> LoadGeometry(Geometry<Vertex> builder) {
        var shapes = CreateGPUGeometry();
        shapes.OverwriteAll(builder);
        return shapes;
    }

    void Draw<ShapeType>(in GPUGeometry<Vertex, ShapeType> shapes, in Vars vars)
        where ShapeType : unmanaged;
}

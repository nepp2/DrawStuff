
using static DrawStuff.ShaderLanguage;

public static partial class BasicShader {

    static Mat4 transform;

    public static void Vertex(in Vec3 pos, out VertexPos vertPos) {
        vertPos =  new Vec4(pos.x, pos.y, pos.z, 1f) * transform;
    }

    public static void Fragment(out RGBA colour) {
        colour = new(1, 1, 1, 1);
    }
}

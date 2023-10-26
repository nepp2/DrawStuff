
using static DrawStuff.ShaderLang;

public static partial class BasicShader {
    public static void Vertex(in StaticVar<Mat4> uTransform, in Vec3 pos, out VertexPos vertPos) {
        vertPos = uTransform.val * new Vec4(pos.x, pos.y, pos.z, 1f);
    }

    public static void Fragment(out RGBA colour, in Vec4 vertPos) {
        colour = new(1, 1, 1, 1);
    }
}

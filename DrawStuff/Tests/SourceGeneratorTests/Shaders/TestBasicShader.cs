
using DrawStuff;
using static DrawStuff.ShaderLanguage;

[ShaderProgram]
public partial class TestBasicShader {

    Mat4 transform;

    public Vec4 Vertex(Vec3 pos) {
        return new Vec4(pos.x, pos.y, pos.z, 1f) * transform;
    }

    public RGBA Fragment() {
        return new(1, 1, 1, 1);
    }
}

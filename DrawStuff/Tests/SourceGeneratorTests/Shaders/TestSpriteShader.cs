
using DrawStuff;
using System.Runtime.InteropServices;
using static DrawStuff.ShaderLanguage;

[StructLayout(LayoutKind.Sequential)]
public record struct SpriteVertex(Vec4 Pos, Vec2 Tc, uint Col);

[ShaderProgram]
partial class TestSpriteShader {

    Mat4 transform;
    Texture2D texture;

    public record struct FragInput(Vec4 Pos, Vec2 TexCoord, RGBA Tint);

    public FragInput Vertex(SpriteVertex v) {
        var tint = vec4(v.Col >> 24, (v.Col >> 16) & 255u, (v.Col >> 8) & 255u, v.Col & 255u) / 255f; 
        return new(transform * vec4(v.Pos.x, v.Pos.y, 0, 1), v.Tc, tint);
    }

    public RGBA Fragment(FragInput f) {
        var tex = sample(texture, f.TexCoord);
        if (tex.a < 0.5f)
            discard();
        return vec4(f.Tint.r * tex.r, f.Tint.g * tex.g, f.Tint.b * tex.b, tex.a);
    }
}

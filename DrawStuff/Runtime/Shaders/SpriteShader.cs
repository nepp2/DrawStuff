
namespace DrawStuff;
using static DrawStuff.ShaderLanguage;

[ShaderProgram]
public partial class SpriteShader {
    Mat4 transform;
    Texture2D texture;

    public record struct ShadeInput(Vec4 Pos, Vec2 TexCoord, RGBA Tint);

    RGBA ToColour(uint col) =>
        vec4(col >> 24, (col >> 16) & 255u, (col >> 8) & 255u, col & 255u) / 255f;

    public ShadeInput Vertex(Vec2 pos, Vec2 tc, uint col) {
        //var tint = vec4(col >> 24, (col >> 16) & 255u, (col >> 8) & 255u, col & 255u) / 255f;
        return new(transform * vec4(pos, 0, 1), tc, ToColour(col));
    }

    public RGBA Fragment(ShadeInput v) {
        var tex = sample(texture, v.TexCoord);
        if (tex.a < 0.5)
            discard();
        return vec4(v.Tint.r * tex.r, v.Tint.g * tex.g, v.Tint.b * tex.b, tex.a);
    }
}
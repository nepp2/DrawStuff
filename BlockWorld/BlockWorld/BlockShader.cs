
using DrawStuff;
using static DrawStuff.ShaderLanguage;

namespace BlockWorld;

// The shader used to the blocks
[ShaderProgram]
public partial class BlockShader {
    Mat4 transform;
    Vec3 lightDir;
    Texture2D tex;

    public record struct FragInput(Vec4 Pos, Vec3 Normal, Vec2 Tc);

    public FragInput Vertex(Vec3 pos, Vec3 normal, Vec2 tc) {
        var tpos = transform * vec4(pos, 1f);
        return new(vec4(tpos.xy, tpos.z * 0.1f, 1), normal, tc);
    }

    public RGBA Fragment(FragInput v) {
        var cosAngle = dot(normalize(v.Normal), normalize(lightDir));
        float facingLight = max(0, cosAngle) * 0.5f;
        float distanceLight = (1f - (0.4f + v.Pos.z * 5)) * 0.25f;
        float ambientLight = 0.25f;
        RGBA colour = sample(tex, v.Tc);
        return vec4(colour.rgb * (facingLight + distanceLight + ambientLight), 1);
    }
}
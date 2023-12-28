
using DrawStuff;
using System.Runtime.InteropServices;
using static DrawStuff.ShaderLanguage;

namespace BlockWorld;

[StructLayout(LayoutKind.Sequential)]
public record struct BlockVertex(Vec3 Pos, Vec3 Normal, Vec2 Tc);

// The shader used to the blocks
[ShaderProgram]
public partial class BlockShader {
    Mat4 transform;
    Vec3 lightDir;
    Texture2D tex;

    public record struct FragInput(Vec4 Pos, Vec3 Normal, Vec2 Tc);

    public FragInput Vertex(BlockVertex v) {
        var tpos = transform * vec4(v.Pos, 1f);
        return new(vec4(tpos.xy, tpos.z * 0.1f, 1), v.Normal, v.Tc);
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
﻿
namespace DrawStuff;

using System.Runtime.InteropServices;
using static DrawStuff.ShaderLanguage;

[StructLayout(LayoutKind.Sequential)]
public record struct SpriteVertex(Vec2 pos, Vec2 tc, uint col);

[ShaderProgram]
public partial class SpriteShader {
    Mat4 transform;
    Texture2D texture;

    public record struct FragInput(Vec4 Pos, Vec2 TexCoord, RGBA Tint);

    RGBA ToColour(uint col) =>
        vec4(col >> 24, (col >> 16) & 255u, (col >> 8) & 255u, col & 255u) / 255f;

    public FragInput Vertex(SpriteVertex v) {
        return new(transform * vec4(v.pos, 0, 1), v.tc, ToColour(v.col));
    }

    public RGBA Fragment(FragInput f) {
        var tex = sample(texture, f.TexCoord);
        if (tex.a < 0.5f)
            discard();
        return vec4(f.Tint.r * tex.r, f.Tint.g * tex.g, f.Tint.b * tex.b, tex.a);
    }
}
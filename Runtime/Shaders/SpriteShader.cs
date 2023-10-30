using static DrawStuff.ShaderLanguage;

namespace DrawStuff {

    [ShaderProgram]
    public static partial class SpriteShader {

        static Mat4 transform;
        static TextureHandle texture;

        public static void Vertex(
            in Vec2 pos,
            in Vec2 tc,
            in uint tint,
            out VertexPos vertPos,
            out Vec2 fragTc,
            out uint fragTint
        ) {
            vertPos = transform * new Vec4(pos.x, pos.y, 0, 1);
            fragTc = tc;
            fragTint = tint;
        }

        public static void Fragment(
            in Vec2 fragTc,
            in uint fragTint,
            out RGBA colour
        ) {
            var tint = new Vec4(
                fragTint >> 24,
                (fragTint >> 16) & 255u,
                (fragTint >> 8) & 255u,
                fragTint & 255u) / 255f;

            var tex = sample(texture, fragTc);
            if (tex.w < 0.5)
                discard();
            colour = new Vec4(
                tint.x * tex.x,
                tint.y * tex.y,
                tint.z * tex.z,
                tex.w);
        }
    }
    
    public partial class SpriteShader2 {

        public record struct VertInput(VertexPos Pos, Vec2 TexCoords, uint Tint);
        public record struct FragInput(VertexPos Pos, Vec2 TexCoords, uint Tint);

        Mat4 transform;
        TextureHandle texture;

        public FragInput Vertex(in VertInput v) {
            var pos = transform * new Vec4(v.Pos.x, v.Pos.y, 0, 1);
            return new (pos, v.TexCoords, v.Tint);
        }
        
        public RGBA Fragment(in FragInput v) {
            var tex = sample(texture, v.TexCoords);
            if (tex.w < 0.5)
                discard();

            var tint = ToRGBA(v.Tint);
            return new Vec4(
                tint.x * tex.x, tint.y * tex.y, tint.z * tex.z, tex.w);
        }

        Vec4 ToRGBA(uint col) =>
            new Vec4(col >> 24, (col >> 16) & 255u, (col >> 8) & 255u, col & 255u) / 255f;

    }
}

namespace DrawStuff;

public static class ShaderLang {

    public struct VertInput {
        public long index;
    }

    public struct StaticVar<T> {
        public T val { get; }
    }

    public struct Mat4 {
        public static Vec4 operator* (Mat4 lhs, Vec4 rhs) =>
            throw new NotImplementedException();
    }
    public record struct Vec4(float x, float y, float z, float w);
    public record struct Vec3(float x, float y, float z) {
        public static Vec3 operator +(Vec3 a, Vec3 b) {
            return a;
        }
    }
    public struct Vec2 { }

    public static float sqrt(float v) => throw new NotImplementedException();

    public record struct RGBA(float r, float g, float b, float a);

    public record struct VertexPos(float x, float y, float z, float w) {
        public static implicit operator VertexPos(Vec4 p) => new(p.x, p.y, p.z, p.w);
        public static implicit operator Vec4(VertexPos p) => new(p.x, p.y, p.z, p.w);
    }
}

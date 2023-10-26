
using Silk.NET.Maths;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace DrawStuff;

public class ShaderLangException : Exception {
    public ShaderLangException() : base("This operation can only execute on the GPU") { }
}

public static class ShaderLang {

    public struct VertInput {
        public long index;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Mat4 {
        public Vec4 Row1;
        public Vec4 Row2;
        public Vec4 Row3;
        public Vec4 Row4;

        public Mat4(Matrix4X4<float> m) {
            Row1 = m.Row1;
            Row2 = m.Row2;
            Row3 = m.Row3;
            Row4 = m.Row4;
        }
        public static Vec4 operator *(Vec4 lhs, Mat4 rhs) => throw new ShaderLangException();
        public static Vec4 operator *(Mat4 lhs, Vec4 rhs) => throw new ShaderLangException();

        public static implicit operator Mat4(Matrix4X4<float> v) => new(v);
    }

    [StructLayout(LayoutKind.Sequential)]
    public record struct Vec4(float x, float y, float z, float w) {
        public Vec4(Vector4D<float> v) : this(v.X, v.Y, v.Z, v.W) { }

        public static Vec4 operator +(Vec4 a, Vec4 b) => throw new ShaderLangException();
        public static Vec4 operator -(Vec4 a, Vec4 b) => throw new ShaderLangException();
        public static Vec4 operator *(Vec4 a, Vec4 b) => throw new ShaderLangException();

        public static implicit operator Vec4(Vector4D<float> v) => new(v);
    }

    [StructLayout(LayoutKind.Sequential)]
    public record struct Vec3(float x, float y, float z) {
        public Vec3(Vector3D<float> v) : this(v.X, v.Y, v.Z) { }

        public static Vec3 operator +(Vec3 a, Vec3 b) => throw new ShaderLangException();
        public static Vec3 operator -(Vec3 a, Vec3 b) => throw new ShaderLangException();
        public static Vec3 operator *(Vec3 a, Vec3 b) => throw new ShaderLangException();

        public static implicit operator Vec3(Vector3D<float> v) => new(v);
    }

    [StructLayout(LayoutKind.Sequential)]
    public record struct Vec2(float x, float y) {
        public Vec2(Vector2D<float> v) : this(v.X, v.Y) { }

        public static Vec2 operator +(Vec2 a, Vec2 b) => throw new ShaderLangException();
        public static Vec2 operator -(Vec2 a, Vec2 b) => throw new ShaderLangException();
        public static Vec2 operator *(Vec2 a, Vec2 b) => throw new ShaderLangException();

        public static implicit operator Vec2(Vector2D<float> v) => new(v);
    }

    public static float sqrt(float v) => throw new NotImplementedException();

    [StructLayout(LayoutKind.Sequential)]
    public record struct RGBA(float r, float g, float b, float a) {
        public RGBA(Vector4D<float> v) : this(v.X, v.Y, v.Z, v.W) { }

        public static RGBA operator +(RGBA a, RGBA b) => throw new ShaderLangException();
        public static RGBA operator -(RGBA a, RGBA b) => throw new ShaderLangException();
        public static RGBA operator *(RGBA a, RGBA b) => throw new ShaderLangException();

        public static implicit operator RGBA(Vector4D<float> v) => new(v);
        public static implicit operator RGBA(Vec4 v) => new(v.x, v.y, v.z, v.w);
    }

    [StructLayout(LayoutKind.Sequential)]
    public record struct VertexPos(float x, float y, float z, float w) {
        public VertexPos(Vector4D<float> v) : this(v.X, v.Y, v.Z, v.W) { }

        public static VertexPos operator +(VertexPos a, VertexPos b) => throw new ShaderLangException();
        public static VertexPos operator -(VertexPos a, VertexPos b) => throw new ShaderLangException();
        public static VertexPos operator *(VertexPos a, VertexPos b) => throw new ShaderLangException();

        public static implicit operator VertexPos(Vector4D<float> v) => new(v);
        public static implicit operator VertexPos(Vec4 v) => new(v.x, v.y, v.z, v.w);
        public static implicit operator Vec4(VertexPos v) => new(v.x, v.y, v.z, v.w);
    }
}

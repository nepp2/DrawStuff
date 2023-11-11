
using Silk.NET.Maths;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace DrawStuff;

public class ShaderLanguageException : Exception {
    public ShaderLanguageException() : base("This operation can only execute on the GPU") { }
}

public interface ShaderLanguage {

    public struct VertInput {
        public long index;
    }

    public struct Texture { }

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

        public Mat4(Matrix4x4 m) {
            Row1 = new(m.M11, m.M12, m.M13, m.M14);
            Row2 = new(m.M21, m.M22, m.M23, m.M24);
            Row3 = new(m.M31, m.M32, m.M33, m.M34);
            Row4 = new(m.M41, m.M42, m.M43, m.M44);
        }

        public static Vec4 operator *(Vec4 lhs, Mat4 rhs) => throw new ShaderLanguageException();
        public static Vec4 operator *(Mat4 lhs, Vec4 rhs) => throw new ShaderLanguageException();

        public static implicit operator Mat4(Matrix4X4<float> v) => new(v);
        public static implicit operator Mat4(Matrix4x4 v) => new(v);
    }

    [StructLayout(LayoutKind.Sequential)]
    public record struct Vec4(float x, float y, float z, float w) {

        public static Vec4 operator +(Vec4 a, Vec4 b) => throw new ShaderLanguageException();
        public static Vec4 operator -(Vec4 a, Vec4 b) => throw new ShaderLanguageException();
        public static Vec4 operator -(Vec4 a) => throw new ShaderLanguageException();
        public static Vec4 operator *(Vec4 a, Vec4 b) => throw new ShaderLanguageException();
        public static Vec4 operator *(Vec4 a, float b) => throw new ShaderLanguageException();
        public static Vec4 operator /(Vec4 a, float b) => throw new ShaderLanguageException();

        public static implicit operator Vec4(Vector4D<float> v) => new(v.X, v.Y, v.Z, v.W);
        public static implicit operator Vec4(Vector4 v) => new(v.X, v.Y, v.Z, v.W);

        public Vec3 xyz => throw new ShaderLanguageException();
        public Vec2 xy => throw new ShaderLanguageException();
        public Vec2 xz => throw new ShaderLanguageException();
        public Vec2 yx => throw new ShaderLanguageException();
        public Vec2 yz => throw new ShaderLanguageException();
        public Vec2 zx => throw new ShaderLanguageException();
        public Vec2 zy => throw new ShaderLanguageException();
    }

    [StructLayout(LayoutKind.Sequential)]
    public record struct Vec3(float x, float y, float z) {

        public static Vec3 operator +(Vec3 a, Vec3 b) => throw new ShaderLanguageException();
        public static Vec3 operator -(Vec3 a, Vec3 b) => throw new ShaderLanguageException();
        public static Vec3 operator -(Vec3 a) => throw new ShaderLanguageException();
        public static Vec3 operator *(Vec3 a, Vec3 b) => throw new ShaderLanguageException();
        public static Vec3 operator *(Vec3 a, float b) => throw new ShaderLanguageException();
        public static Vec3 operator /(Vec3 a, float b) => throw new ShaderLanguageException();

        public static implicit operator Vec3(Vector3D<float> v) => new(v.X, v.Y, v.Z);
        public static implicit operator Vec3(Vector3 v) => new(v.X, v.Y, v.Z);

        public Vec2 xy => throw new ShaderLanguageException();
        public Vec2 xz => throw new ShaderLanguageException();
        public Vec2 yx => throw new ShaderLanguageException();
        public Vec2 yz => throw new ShaderLanguageException();
        public Vec2 zx => throw new ShaderLanguageException();
        public Vec2 zy => throw new ShaderLanguageException();
    }

    [StructLayout(LayoutKind.Sequential)]
    public record struct Vec2(float x, float y) {
        
        public static Vec2 operator +(Vec2 a, Vec2 b) => throw new ShaderLanguageException();
        public static Vec2 operator -(Vec2 a, Vec2 b) => throw new ShaderLanguageException();
        public static Vec2 operator -(Vec2 a) => throw new ShaderLanguageException();
        public static Vec2 operator *(Vec2 a, Vec2 b) => throw new ShaderLanguageException();
        public static Vec2 operator *(Vec2 a, float b) => throw new ShaderLanguageException();
        public static Vec2 operator /(Vec2 a, float b) => throw new ShaderLanguageException();

        public static implicit operator Vec2(Vector2D<float> v) => new(v.X, v.Y);
        public static implicit operator Vec2(Vector2 v) => new(v.X, v.Y);
    }

    [StructLayout(LayoutKind.Sequential)]
    public record struct RGBA(float r, float g, float b, float a) {
        public RGBA(Vector4D<float> v) : this(v.X, v.Y, v.Z, v.W) { }

        public static RGBA operator +(RGBA a, RGBA b) => throw new ShaderLanguageException();
        public static RGBA operator -(RGBA a, RGBA b) => throw new ShaderLanguageException();
        public static RGBA operator *(RGBA a, RGBA b) => throw new ShaderLanguageException();
        public static RGBA operator *(RGBA a, float b) => throw new ShaderLanguageException();
        public static RGBA operator /(RGBA a, float b) => throw new ShaderLanguageException();

        public static implicit operator RGBA(Vector4D<float> v) => new(v);
        public static implicit operator RGBA(Vec4 v) => new(v.x, v.y, v.z, v.w);
    }

    public static Vec2 vec2(float x, float y) => throw new NotImplementedException();

    public static Vec3 vec3(float x, float y, float z) => throw new NotImplementedException();
    public static Vec3 vec3(Vec2 xy, float z) => throw new NotImplementedException();
    public static Vec3 vec3(float x, Vec2 yz) => throw new NotImplementedException();

    public static Vec4 vec4(float x, float y, float z, float w) => throw new NotImplementedException();
    public static Vec4 vec4(Vec3 xyz, float w) => throw new NotImplementedException();
    public static Vec4 vec4(float x, Vec3 yzw) => throw new NotImplementedException();
    public static Vec4 vec4(Vec2 xy, float z, float w) => throw new NotImplementedException();
    public static Vec4 vec4(float x, float y, Vec2 zw) => throw new NotImplementedException();
    public static Vec4 vec4(Vec2 xy, Vec2 zw) => throw new NotImplementedException();
    public static Vec4 vec4(float x, Vec2 yz, float w) => throw new NotImplementedException();

    public static RGBA rgba(float r, float g, float b, float a) => throw new NotImplementedException();

    public static float sqrt(float v) => throw new NotImplementedException();
    public static float cos(float v) => throw new NotImplementedException();
    public static float sin(float v) => throw new NotImplementedException();
    public static float tan(float v) => throw new NotImplementedException();
    public static float acos(float v) => throw new NotImplementedException();
    public static float asin(float v) => throw new NotImplementedException();

    public static RGBA sample(Texture t, Vec2 tc) => throw new NotImplementedException();

    public static Vec2 normalize(Vec2 v) => throw new NotImplementedException();
    public static Vec3 normalize(Vec3 v) => throw new NotImplementedException();
    public static Vec4 normalize(Vec4 v) => throw new NotImplementedException();

    public static float dot(Vec2 a, Vec2 b) => throw new NotImplementedException();
    public static float dot(Vec3 a, Vec3 b) => throw new NotImplementedException();
    public static float dot(Vec4 a, Vec4 b) => throw new NotImplementedException();

    public static float max(float a, float b) => throw new NotImplementedException();
    public static float min(float a, float b) => throw new NotImplementedException();

    public static Vec4 discard() => throw new NotImplementedException();

}

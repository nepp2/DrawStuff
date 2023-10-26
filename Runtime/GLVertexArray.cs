
using Silk.NET.OpenGL;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DrawStuff;

[StructLayout(LayoutKind.Sequential)]
public record struct TriangleIndices(uint A, uint B, uint C);

public record struct GLAttribPtrType(VertexAttribPointerType Type, int ByteSize) {
    public static GLAttribPtrType Float32 => new(VertexAttribPointerType.Float, 4);
    public static GLAttribPtrType Uint32 => new(VertexAttribPointerType.UnsignedInt, 4);
    public static GLAttribPtrType Int32 => new(VertexAttribPointerType.Int, 4);
    public static GLAttribPtrType Uint8 => new(VertexAttribPointerType.UnsignedByte, 1);

    static GLAttribPtrType() {
        // Assert that these enums match because VertexAttribPointer uses one and VertexAttribIPointer uses the other
        Validation.Assert((int)VertexAttribPointerType.Float == (int)GLEnum.Float);
        Validation.Assert((int)VertexAttribPointerType.UnsignedInt == (int)GLEnum.UnsignedInt);
        Validation.Assert((int)VertexAttribPointerType.Int == (int)GLEnum.Int);
        Validation.Assert((int)VertexAttribPointerType.UnsignedByte == (int)GLEnum.UnsignedByte);
    }
}

public record struct GLAttribute(string Name, int NumVals, GLAttribPtrType ValInfo) {

    public static GLAttribPtrType InferAttribPtrType(Type t) {
        Validation.Assert(t.IsValueType, "Can only pass value types as shader attributes");
        if (t.IsPrimitive) {
            if (t == typeof(float))
                return GLAttribPtrType.Float32;
            else if (t == typeof(uint))
                return GLAttribPtrType.Uint32;
            else if(t == typeof(int))
                return GLAttribPtrType.Int32;
            else if (t == typeof(byte))
                return GLAttribPtrType.Uint8;
        }
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            return InferAttribPtrType(f.FieldType);
        throw new ValidationException($"Unsupported type {t}");
    }

    public static GLAttribute Of(string name, Type t) {
        var attribType = InferAttribPtrType(t);
        return new(name, Marshal.SizeOf(t) / attribType.ByteSize, attribType);
    }

    public static GLAttribute Of<T>(string name) where T : unmanaged {
        return Of(name , typeof(T));
    }

    private static int FieldOffset(FieldInfo f) {
        var offset = (int)Marshal.OffsetOf(f.DeclaringType!, f.Name);
        return offset;
    }

    public static GLAttribute[] InferAttribArray(Type t) {
        Validation.Assert(t.IsValueType, "Can only pass value types as shader attributes");
        return t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .OrderBy(FieldOffset)
            .Select(f => Of(f.Name, f.FieldType))
            .ToArray();
    }

    public static GLAttribute[] InferAttribArray<T>() where T : unmanaged {
        return InferAttribArray(typeof(T));
    }
}

public static class GLVertexArray {
    public static GLVertexArray<VBO, EBO> Create<VBO, EBO>(
        GL gl, GLBufferObject<VBO> vbo, GLBufferObject<EBO> ebo)
        where VBO : unmanaged where EBO : unmanaged
            => new(gl, vbo, ebo);

    public static GLTriangleArray<VBO> Create<VBO>(
        GL gl, GLBufferObject<VBO> vbo)
        where VBO : unmanaged
            => new(gl, vbo, new (gl, BufferTargetARB.ElementArrayBuffer));
}

public class GLVertexArray<VBO, EBO> : IDisposable where VBO : unmanaged where EBO : unmanaged {

    private GL gl;
    public uint Handle { get; }
    public GLBufferObject<VBO> Vbo { get; }
    public GLBufferObject<EBO> Ebo { get; }

    private GLAttribute[] Attribs;

    public GLVertexArray(GL gl, GLBufferObject<VBO> vbo, GLBufferObject<EBO> ebo) {
        this.gl = gl;
        Handle = gl.GenVertexArray();
        Vbo = vbo;
        Ebo = ebo;

        Validation.Assert(Vbo.Target == BufferTargetARB.ArrayBuffer);
        Validation.Assert(Ebo.Target == BufferTargetARB.ElementArrayBuffer);

        gl.BindVertexArray(Handle);
        Vbo.Bind();
        Ebo.Bind();

        Attribs = GLAttribute.InferAttribArray<VBO>();
        SetAttributeLayout(gl, Attribs);
        gl.EnableVertexAttribArray(0);
    }

    //Tell opengl how to give the attribute data to the shaders.
    private static unsafe void SetAttributeLayout(GL gl, ReadOnlySpan<GLAttribute> attributes) {
        uint stride = 0;
        foreach(var a in attributes)
            stride += (uint)(a.NumVals * a.ValInfo.ByteSize);

        uint index = 0;
        int byteOffset = 0;
        foreach (var a in attributes) {
            gl.EnableVertexAttribArray(index);
            if (a.ValInfo.Type == VertexAttribPointerType.Float) {
                gl.VertexAttribPointer(index, a.NumVals, a.ValInfo.Type, false, stride, (void*)byteOffset);
            }
            else {
                gl.VertexAttribIPointer(index, a.NumVals, (GLEnum)a.ValInfo.Type, stride, (void*)byteOffset);
            }
            index += 1;
            byteOffset += a.NumVals * a.ValInfo.ByteSize;
        }
    }

    public unsafe void Draw(int indexCount) {
        gl.BindVertexArray(Handle);
        Vbo.Bind();
        Ebo.Bind();
        int minCount = (sizeof(EBO) / 4) * Ebo.Count;
        gl.DrawElements(PrimitiveType.Triangles, (uint)Math.Min(minCount, indexCount), DrawElementsType.UnsignedInt, null);
    }

    public void Dispose() {
        gl.DeleteVertexArray(Handle);
        Vbo.Dispose();
        Ebo.Dispose();
    }
}

public class GLTriangleArray<VBO> : GLVertexArray<VBO, TriangleIndices> where VBO : unmanaged {
    public GLTriangleArray(GL gl, GLBufferObject<VBO> vbo, GLBufferObject<TriangleIndices> ebo) : base(gl, vbo, ebo) {

    }
}

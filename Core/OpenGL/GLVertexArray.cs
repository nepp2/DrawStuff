
using Silk.NET.OpenGL;

namespace DrawStuff.OpenGL;

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

public record struct GLAttribute(string Name, int NumVals, GLAttribPtrType ValInfo);

public class GLVertexArray<VBO, EBO> : IDisposable where VBO : unmanaged where EBO : unmanaged {

    private GL gl;
    public uint Handle { get; }
    public GLBufferObject<VBO> Vbo { get; }
    public GLBufferObject<EBO> Ebo { get; }

    private GLAttribute[] Attribs;

    public GLVertexArray(GL gl, GLBufferObject<VBO> vbo, GLBufferObject<EBO> ebo, GLAttribute[] attribs) {
        this.gl = gl;
        Handle = gl.GenVertexArray();
        Vbo = vbo;
        Ebo = ebo;

        Validation.Assert(Vbo.Target == BufferTargetARB.ArrayBuffer);
        Validation.Assert(Ebo.Target == BufferTargetARB.ElementArrayBuffer);

        gl.BindVertexArray(Handle);
        Vbo.Bind();
        Ebo.Bind();

        Attribs = attribs;
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
    }
}

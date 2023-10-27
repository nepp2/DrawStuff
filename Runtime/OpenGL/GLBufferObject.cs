

using Silk.NET.OpenGL;
namespace DrawStuff;

public class GLBufferObject<T> : IDisposable where T : unmanaged {
    private GL gl;
    public BufferTargetARB Target { get; }
    public uint Handle { get; }
    public int Count { get; private set; } = 0;
    public int Capacity { get; private set; } = 0;

    public GLBufferObject(GL gl, BufferTargetARB target) {
        this.gl = gl;
        Handle = gl.GenBuffer();
        Target = target;
    }

    public void Bind() {
        gl.BindBuffer(Target, Handle);
    }

    public unsafe void UpdateBuffer(ReadOnlySpan<T> values, BufferUsageARB usage = BufferUsageARB.DynamicDraw) {
        Bind();
        fixed (void* v = &values[0]) {
            gl.BufferData(Target, (nuint)(values.Length * sizeof(T)), null, usage);
            gl.BufferData(Target, (nuint)(values.Length * sizeof(T)), v, usage);
        }
        Count = values.Length;
        Capacity = Math.Max(Capacity, values.Length);
    }

    public void Dispose() {
        gl.DeleteBuffer(Handle);
    }
}

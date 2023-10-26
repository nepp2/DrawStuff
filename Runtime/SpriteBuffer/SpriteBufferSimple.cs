
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DrawStuff;

public class SpriteBufferSimple : IDisposable {

    [StructLayout(LayoutKind.Sequential)]
    record struct VertPos(float X, float Y);

    [StructLayout(LayoutKind.Sequential)]
    record struct TexCoord(float X, float Y);

    [StructLayout(LayoutKind.Sequential)]
    record struct TexVert(VertPos Pos, TexCoord Tex, uint Col);

    [StructLayout(LayoutKind.Sequential)]
    record struct IndexTriangle(uint A, uint B, uint C);

    private static GLShader Shader = null!;

    GLVertexArray<TexVert, IndexTriangle> VertexArray;

    private GL gl { get; }
    private GLTexture Atlas { get; }

    ValueBuffer<TexVert> verts = new();
    ValueBuffer<IndexTriangle> inds = new();

    public void Clear() {
        verts.Clear();
    }

    public ReadOnlySpan<float> GetVertices() => MemoryMarshal.Cast<TexVert, float>(verts.AsSpan());
    public ReadOnlySpan<uint> GetIndices() => MemoryMarshal.Cast<IndexTriangle, uint>(inds.AsSpan());

    private SpriteBufferSimple(GL gl, GLTexture atlas) {
        this.gl = gl;
        Atlas = atlas;
        var vbo = new GLBufferObject<TexVert>(gl, BufferTargetARB.ArrayBuffer);
        var ebo = new GLBufferObject<IndexTriangle>(gl, BufferTargetARB.ElementArrayBuffer);
        VertexArray = new (gl, vbo, ebo);
    }

    public static SpriteBufferSimple Create(GL gl, GLTexture atlas) {
        if (Shader == null) {
            Shader = GLShader.Compile(gl, VertexShaderSrc(), FragmentShaderSrc());
            // Get texture uniform and set it to 0th slot
            int location = gl.GetUniformLocation(Shader.Handle, "uTexture");
            gl.Uniform1(location, 0);
            int transformLocation = gl.GetUniformLocation(Shader.Handle, "uTransform");
            gl.Uniform1(transformLocation, 0);
        }
        return new(gl, atlas);
    }

    public void Draw(in Matrix4X4<float> transform) {
        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        VertexArray.Vbo.UpdateBuffer(verts.AsSpan());
        var vertsIndexed = inds.Count * 2;
        if (vertsIndexed < verts.Count) {
            for (uint i = (uint)vertsIndexed; i < verts.Count; i += 4) {
                PushTriangle(i + 0, i + 1, i + 3);
                PushTriangle(i + 1, i + 2, i + 3);
            }
            VertexArray.Ebo.UpdateBuffer(inds.AsSpan());
        }

        Shader.Bind();
        Shader.SetUniform("uTransform", transform);
        Atlas.Bind(TextureUnit.Texture0);
        int trianglesNeeded = verts.Count / 2;
        VertexArray.Draw(trianglesNeeded * 3);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    void PushTriangle(uint a, uint b, uint c) {
        ref var i = ref inds.Push();
        i.A = a;
        i.B = b;
        i.C = c;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    void PushVert(float x, float y, float tx, float ty, Colour c) {
        ref var v = ref verts.Push();
        v.Pos = new(x, y);
        v.Tex = new(tx, ty);
        v.Col = c.RGBA;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public void PushQuad(float x, float y, float w, float h, Colour c) {
        var (x2, y2) = (x + w, y + h);
        PushVert(x2, y2, 1, 1, c);
        PushVert(x2, y, 1, 0, c);
        PushVert(x, y, 0, 0, c);
        PushVert(x, y2, 0, 1, c);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void PushQuad(float x, float y, float w, float h, float tx, float ty, float tw, float th, Colour c) {
        var (x2, y2) = (x + w, y + h);
        var (tx2, ty2) = (tx + tw, ty + th);
        PushVert(x2, y2, tx2, ty2, c);
        PushVert(x2, y, tx2, ty, c);
        PushVert(x, y, tx, ty, c);
        PushVert(x, y2, tx, ty2, c);
    }

    public void Dispose() {
        VertexArray.Dispose();
    }

    private static string VertexShaderSrc() =>
        BundledData.GetTextFile("SpriteBuffer/SpriteBufferSimple.vert");

    private static string FragmentShaderSrc() =>
        BundledData.GetTextFile("SpriteBuffer/SpriteBufferSimple.frag");
}

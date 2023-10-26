
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DrawStuff;

public class SpriteBuffer : IDisposable {

    [StructLayout(LayoutKind.Sequential)]
    record struct Vec2(float X, float Y);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    record struct Rect(Vec2 Pos, Vec2 Size);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    record struct SpriteData(Rect Bounds, Rect TexBounds, uint Col);

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 12)]
    record struct IndexTriangle(uint A, uint B, uint C);

    record struct EmptyVertex();

    private static GLShader Shader = null!;

    GLVertexArray<EmptyVertex, IndexTriangle> VertexArray;
    GLBufferObject<SpriteData> SpriteDataBuffer;

    private GL gl { get; }
    private GLTexture Atlas { get; }

    ValueBuffer<IndexTriangle> indexTriangles = new();
    ValueBuffer<SpriteData> sprites = new();
    
    public void Clear() {
        sprites.Clear();
    }

    private SpriteBuffer(GL gl, GLTexture atlas) {
        this.gl = gl;
        Atlas = atlas;
        var vbo = new GLBufferObject<EmptyVertex>(gl, BufferTargetARB.ArrayBuffer);
        var ebo = new GLBufferObject<IndexTriangle>(gl, BufferTargetARB.ElementArrayBuffer);
        VertexArray = new (gl, vbo, ebo);
        SpriteDataBuffer = new GLBufferObject<SpriteData>(gl, BufferTargetARB.ShaderStorageBuffer);
        SpriteDataBuffer.Bind();
    }

    public static SpriteBuffer Create(GL gl, GLTexture atlas) {
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
        SpriteDataBuffer.UpdateBuffer(sprites.AsReadOnlySpan(), BufferUsageARB.DynamicCopy);
        var spritesIndexed = indexTriangles.Count / 2;
        if (spritesIndexed < sprites.Count) {
            for (uint si = (uint)spritesIndexed; si < sprites.Count; ++si) {
                uint i = si * 4;
                indexTriangles.Push(new (i + 0, i + 1, i + 3));
                indexTriangles.Push(new (i + 1, i + 2, i + 3));
            }
            VertexArray.Ebo.UpdateBuffer(indexTriangles.AsSpan());
        }

        Shader.Bind();
        Shader.SetUniform("uTransform", transform);
        Shader.BindStorageBuffer(3, SpriteDataBuffer);
        Atlas.Bind(TextureUnit.Texture0);
        VertexArray.Draw(indexTriangles.Count * 3);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public void PushQuad(float x, float y, float w, float h, Colour c) {
        PushQuad(x, y, w, h, 0, 0, 1, 1, c);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void PushQuad(float x, float y, float w, float h, float tx, float ty, float tw, float th, Colour c) {
        ref var s = ref sprites.Push();
        s.Bounds = new(new(x, y), new(w, h));
        s.TexBounds = new(new(tx, ty), new(tw, th));
        s.Col = c.RGBA;
    }

    public void Dispose() {
        VertexArray.Dispose();
    }

    private static string VertexShaderSrc() =>
        BundledData.GetTextFile("SpriteBuffer/SpriteBufferSSBO.vert");

    private static string FragmentShaderSrc() =>
        BundledData.GetTextFile("SpriteBuffer/SpriteBufferSSBO.frag");
}

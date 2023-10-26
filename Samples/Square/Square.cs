
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Maths;

using System.Runtime.InteropServices;

using DrawStuff;
using static DrawStuff.ShaderLang;

var (width, height) = (2500, 1500);

var options = WindowOptions.Default;
options.Size = new Vector2D<int>(width, height);
options.Title = "BoxWorld";
options.VSync = true;
using var window = Window.Create(options);

GLVertexArray<BasicShader.VertexInputs, IndexTriangle> GenerateSquare(GL gl) {
    var vertBuf = new GLBufferObject<BasicShader.VertexInputs>(gl, BufferTargetARB.ArrayBuffer);
    var indBuf = new GLBufferObject<IndexTriangle>(gl, BufferTargetARB.ElementArrayBuffer);
    var geometry = GLVertexArray.Create(gl, vertBuf, indBuf);

    var verts = new ValueBuffer<BasicShader.VertexInputs>();
    var indices = new ValueBuffer<IndexTriangle>();

    verts.Push(new(new(1f, 1f, 0f)));
    verts.Push(new(new(1f, 0f, 0f)));
    verts.Push(new(new(0f, 1f, 0f)));
    verts.Push(new(new(0f, 0f, 0f)));
    indices.Push(new IndexTriangle(0, 1, 2));
    indices.Push(new IndexTriangle(1, 2, 3));

    vertBuf.UpdateBuffer(verts.AsSpan());
    indBuf.UpdateBuffer(indices.AsSpan());
    return geometry;
}

void KeyDown(IKeyboard arg1, Key arg2, int arg3) {
    if (arg2 == Key.Escape) {
        window.Close();
    }
}

void OnLoad() {
    IInputContext input = window.CreateInput();
    for (int i = 0; i < input.Keyboards.Count; i++) {
        input.Keyboards[i].KeyDown += KeyDown;
    }

    var gl = GL.GetApi(window);

    var geometry = GenerateSquare(gl);

    var shader = GLShader.Compile(gl, BasicShader.VertexSource, BasicShader.FragmentSource);

    Console.WriteLine($"Generated vert source: {BasicShader.VertexSource}");
    Console.WriteLine($"Generated frag source: {BasicShader.FragmentSource}");

    void OnRender(double seconds) {

        float size = 200;

        gl.Clear((uint)ClearBufferMask.ColorBufferBit | (uint)ClearBufferMask.DepthBufferBit);
        var transform =
            // Scale square up to pixel size
            Matrix4X4.CreateScale(new Vector3D<float>(size, size, 1f))
            // Move square to center of screen (in pixel coordinates)
            * Matrix4X4.CreateTranslation((width - size) / 2f, (height - size) / 2f, 0)
            // Scale units to match pixel size, and flip Y axis
            * Matrix4X4.CreateScale(new Vector3D<float>(2f / width, -2f / height, 1f))
            // Move the origin to top left corner
            * Matrix4X4.CreateTranslation(-1f, 1f, 0);

        shader.Bind();
        shader.SetUniform("transform", transform);
        geometry.Draw(geometry.Ebo.Count * 3);
    }

    void OnUpdate(double obj) {
        
    }

    void OnClose() {}

    window.Render += OnRender;
    window.Update += OnUpdate;
    window.Closing += OnClose;
}

window.Load += OnLoad;

window.Run();

[StructLayout(LayoutKind.Sequential)]
record struct IndexTriangle(uint A, uint B, uint C);

[ShaderProgram]
public static partial class BasicShader {

    static Mat4 transform;

    public static void Vertex(in Vec3 pos, out VertexPos vertPos) {
        vertPos = transform * new Vec4(pos.x, pos.y, pos.z, 1f);
    }

    public static void Fragment(out RGBA colour) {
        colour = new(1, 1, 1, 1);
    }
}

public interface IShader<ConfigData, VertexInput>
    where ConfigData : unmanaged
    where VertexInput : unmanaged
{}

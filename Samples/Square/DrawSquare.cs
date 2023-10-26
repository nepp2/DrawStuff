
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Maths;

using DrawStuff;
using static DrawStuff.ShaderLanguage;

var (width, height) = (2500, 1500);

var options = WindowOptions.Default;
options.Size = new Vector2D<int>(width, height);
options.Title = "BoxWorld";
options.VSync = true;
using var window = Window.Create(options);

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
    var pipeline = RenderPipeline.Create(gl, BasicShader.PipelineConfig);

    // Generate a square
    var verts = pipeline.CreateVertexBuffer();
    var indices = pipeline.CreateIndexBuffer();
    verts.Push(new Vec3(1f, 1f, 0f));
    verts.Push(new Vec3(1f, 0f, 0f));
    verts.Push(new Vec3(0f, 1f, 0f));
    verts.Push(new Vec3(0f, 0f, 0f));
    indices.Push(new TriangleIndices(0, 1, 2));
    indices.Push(new TriangleIndices(1, 2, 3));
    pipeline.SetVertexData(verts.AsSpan());
    pipeline.SetIndices(indices.AsSpan());

    void OnRender(double seconds) {

        float size = 200;

        gl.Clear((uint)ClearBufferMask.ColorBufferBit | (uint)ClearBufferMask.DepthBufferBit);

        Mat4 transform =
            Matrix4X4.CreateScale(new Vector3D<float>(size, size, 1f))
            * Matrix4X4.CreateTranslation((width - size) / 2f, (height - size) / 2f, 0)
            * Matrix4X4.CreateScale(new Vector3D<float>(2f / width, -2f / height, 1f))
            * Matrix4X4.CreateTranslation(-1f, 1f, 0);

        pipeline.Render(transform);
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

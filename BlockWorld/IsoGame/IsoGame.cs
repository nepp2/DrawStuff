using Silk.NET.Windowing;
using DrawStuff;
using System.Numerics;
using Silk.NET.Input;
using System.Drawing;
using BlockWorld;

IWindow CreateWindow() {
    var window = Window.Create(WindowOptions.Default with {
        Title = "IsoGame",
        Size = new(1500, 1500),
        VSync = true,
    });
    return window;
}

GameLoopHandlers Start(IWindow window) {

    // Start DrawStuff and load the shader (it's defined at the bottom of this file)
    var ds = IDrawStuff.StartDrawing(window);
    var shader = ds.LoadShader(BlockShader.Config);

    // Load a texture atlas
    var atlas = BlockAtlas.Load(ds);

    BlockVertex toVertex(TexturedVertex v) =>
        new(v.Pos, v.Normal, v.TexCoord);

    // Generate some cube chunks
    var world = new Geometry<BlockVertex>();
    for (int x = -50; x < 50; ++x) {
        for (int z = -50; z < 50; ++z) {
            world.AddQuadFace(
                new(x, 0, z),
                new(x + 1, 0, z),
                new(x + 1, 0, z + 1),
                new(x, 0, z + 1),
                atlas.GrassTop, toVertex);
        }
    }

    // Load to GPU
    var gpuWorld = shader.LoadGeometry(world);

    // Some text
    var fontShader = ds.LoadShader(SpriteShader.Config);
    var font = ds.LoadDefaultFont(32);

    // Add some geometry for the characters
    var textQuads = fontShader.CreateGeometry();
    var textGeometry = fontShader.CreateGPUGeometry();

    var keyboard = window.CreateInput().Keyboards[0];

    float worldPixelScale = 1500f;

    var cam = new KeyboardCamera();
    cam.ZoomSpeed = 2;
    cam.Speed = 30;
    cam.Angle = 5 * (MathF.PI / 4f);
    cam.Pos = Vector2.One;

    void OnRender(double seconds) {
        // Clear the screen
        ds.ClearWindow(Color.LightSkyBlue);

        var screenSize = window.FramebufferSize;

        float baseScale = 0.1f * cam.ZoomFactor;
        float xScale = (worldPixelScale / screenSize.X) * baseScale * 0.9f;

        float yScale = (worldPixelScale / screenSize.Y) * baseScale;

        var transform =
            Matrix4x4.CreateTranslation(cam.Pos.X, 0, cam.Pos.Y)
            * Matrix4x4.CreateFromQuaternion(
                Quaternion.CreateFromYawPitchRoll(cam.Angle, 0, 0))
            * Matrix4x4.CreateFromQuaternion(
                Quaternion.CreateFromYawPitchRoll(0, -0.5f, 0))
            * Matrix4x4.CreateScale(new Vector3(xScale, yScale, baseScale));

        // Draw the blocks
        shader.Draw(gpuWorld, new(transform, new(-1, -1, -1), atlas.Tex));

        textQuads.Clear();
        var textRect = textQuads.AddText(new(0, 0), font,
            $"{world.VertexCount} vertices, {(int)(1 / seconds),3} fps");
        textGeometry.OverwriteAll(textQuads);

        var textTransform =
            Matrix4x4.CreateTranslation(window.FramebufferSize.X - textRect.Width, 0, 0)
            * ds.GetPixelCamera();

        ds.ClearDepth();
        fontShader.Draw(textGeometry, new(textTransform, font.Texture));
    }

    void OnUpdate(double seconds) {
        cam.Update(keyboard, seconds);
    };

    return new(OnUpdate, OnRender);
}

ReloadHandler.StartReloadingWindow(CreateWindow, Start);

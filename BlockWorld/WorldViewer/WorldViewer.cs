using Silk.NET.Windowing;
using DrawStuff;
using System.Numerics;
using DotnetNoise;
using Silk.NET.Input;
using System.Drawing;
using BlockWorld;

IWindow CreateWindow() {
    var window = Window.Create(WindowOptions.Default with {
        Title = "Block World",
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

    // Generate some cube chunks
    var noise = new FastNoise(1342) { UsedNoiseType = FastNoise.NoiseType.Perlin };
    var world = new Geometry<BlockVertex>();
    int chunkSize = 16;
    for (int x = -20; x < 20; ++x) {
        for (int z = -20; z < 20; ++z) {
            var offset = new Vector3(x * 16, 0, z * 16);
            var chunk = Chunk.Generate(noise, offset);
            world.GenerateChunkGeometry(atlas, chunk, offset);
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
    cam.Pos = Vector2.One * -(chunkSize / 2f + 0.5f);

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

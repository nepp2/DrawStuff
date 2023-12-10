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

    // Load a texture atlas
    var atlas = BlockAtlas.Load(ds);

    // Load the sprite shader
    var shader = ds.LoadShader(SpriteShader.Config);

    // Generate some cube chunks
    var noise = new FastNoise(1342) { UsedNoiseType = FastNoise.NoiseType.Perlin };
    var canvas = shader.CreateGeometry();
    int gridSize = 100;
    float tileSize = 50f;
    var tc = atlas.Dirt;
    for (int x = 0; x < gridSize; ++x) {
        for (int y = 0; y < gridSize; ++y) {
            canvas.AddQuad(new(x * tileSize, y * tileSize, tileSize, tileSize), tc,
                (xp, yp, tc) =>
                    new(new(xp, yp), tc, Colour.White.RGBA));
        }
    }
    canvas.AddQuad(0, 0, 100f, 100f, 0, 0, 1, 1,
        (xp, yp, xtc, ytc) =>
            new(new(xp, yp), new(xtc, ytc), Colour.White.RGBA));
    canvas.AddQuad(new(100f, 0, 100f, 100f), tc,
        (xp, yp, tc) =>
            new(new(xp, yp), tc, Colour.White.RGBA));


    // Load to GPU
    var gpuWorld = shader.LoadGeometry(canvas);

    // Some text
    var font = ds.LoadDefaultFont(32);

    // Add some geometry for the characters
    var textQuads = shader.CreateGeometry();
    var textGeometry = shader.CreateGPUGeometry();
    var keyboard = window.CreateInput().Keyboards[0];
    var controls = new Controls();
    void OnRender(double seconds) {
        // Clear the screen
        ds.ClearWindow(Color.LightSkyBlue);

        var size = window.FramebufferSize;

        // Draw the tiles
        var transform =
            Matrix4x4.CreateTranslation(controls.Pos.X, controls.Pos.Y, 0f)
            * ds.GetPixelCamera()
            * Matrix4x4.CreateScale(controls.ZoomFactor);
        shader.Draw(gpuWorld, new(transform, atlas.Tex));

        // Draw the text
        textQuads.Clear();
        var textRect = textQuads.AddText(new(0, 0), font,
        $"{canvas.VertexCount} vertices, {(int)(1 / seconds),3} fps");
        textGeometry.OverwriteAll(textQuads);
        var textTransform =
            Matrix4x4.CreateTranslation(window.FramebufferSize.X - textRect.Width, 0, 0)
            * ds.GetPixelCamera();

        ds.ClearDepth();
        shader.Draw(textGeometry, new(textTransform, font.Texture));
    }

    void OnUpdate(double seconds) {
        controls.Update(keyboard, seconds);
    };

    return new(OnUpdate, OnRender);
}

ReloadHandler.StartReloadingWindow(CreateWindow, Start);

class Controls {

    public float ZoomPower = 0;
    public float ZoomSpeed = 2;
    public float Speed = 1000;
    public float Angle = 0;
    public Vector2 Pos = Vector2.Zero;

    public float ZoomFactor => MathF.Pow(2, ZoomPower);

    public void Update(IKeyboard keyboard, double seconds) {
        var zoomedSpeed = Speed / MathF.Pow(2, ZoomPower / 2);
        if (keyboard.IsKeyPressed(Key.Left))
            Pos += new Vector2(MathF.Cos(Angle), MathF.Sin(Angle)) * (float)seconds * zoomedSpeed;
        if (keyboard.IsKeyPressed(Key.Right))
            Pos -= new Vector2(MathF.Cos(Angle), MathF.Sin(Angle)) * (float)seconds * zoomedSpeed;
        if (keyboard.IsKeyPressed(Key.Up))
            Pos += new Vector2(MathF.Sin(-Angle), MathF.Cos(-Angle)) * (float)seconds * zoomedSpeed;
        if (keyboard.IsKeyPressed(Key.Down))
            Pos -= new Vector2(MathF.Sin(-Angle), MathF.Cos(-Angle)) * (float)seconds * zoomedSpeed;
        if (keyboard.IsKeyPressed(Key.W))
            ZoomPower += ZoomSpeed * (float)seconds;
        if (keyboard.IsKeyPressed(Key.S))
            ZoomPower -= ZoomSpeed * (float)seconds;
        if (keyboard.IsKeyPressed(Key.A))
            Angle += (float)seconds;
        if (keyboard.IsKeyPressed(Key.D))
            Angle -= (float)seconds;
    }
}


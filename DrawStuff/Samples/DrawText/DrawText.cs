using Silk.NET.Windowing;
using DrawStuff;
using System.Numerics;

// Create a window
var window = Window.Create(WindowOptions.Default with {
    Title = "DrawText",
    Size = new(1000, 1000),
    VSync = true,
});

void OnWindowLoad() {
    // Start DrawStuff and load the built-in sprite shader
    var ds = IDrawStuff.StartDrawing(window);
    var shader = ds.LoadShader(SpriteShader.Config);
    var font = ds.LoadDefaultFont(32);

    // Add some geometry for the characters
    var spriteCanvas = shader.CreateGeometry();
    spriteCanvas.AddText(new(100, 100), font, "Hello world");
    var gpuGeometry = shader.LoadGeometry(spriteCanvas);

    double time = 0;

    void OnRender(double seconds) {
        time += seconds;
        ds.ClearWindow();
        var translate =
            Matrix4x4.CreateScale((1.2f + MathF.Sin((float)time)) * 3f)
            * ds.GetPixelCamera();
        shader.Draw(gpuGeometry, new(translate, font.Texture));
    }

    window.Render += OnRender;
}

window.Load += OnWindowLoad;
window.Run();

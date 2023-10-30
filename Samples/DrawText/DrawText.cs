using Silk.NET.Windowing;
using DrawStuff;

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
    var font = ds.LoadDefaultFont();

    // Add some geometry for the characters
    var builder = shader.CreateTriangleBuilder();
    Font.DrawText(builder, new(100, 100), font, "Hello world");
    var textGeometry = shader.CreateTriangleArray(builder);

    void OnRender(double seconds) {
        ds.ClearWindow();
        shader.Draw(textGeometry, new(ds.GetPixelCamera(), font.Texture));
    }

    window.Render += OnRender;
}

window.Load += OnWindowLoad;
window.Run();

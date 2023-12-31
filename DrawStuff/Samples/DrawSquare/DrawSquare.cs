﻿using Silk.NET.Windowing;
using DrawStuff;
using static DrawStuff.ShaderLanguage;
using System.Numerics;

// the size of the square
const float size = 200;

// Create a window
var window = Window.Create(WindowOptions.Default with {
    Title = "DrawSquare",
    Size = new(1000, 1000),
    VSync = true,
});

void OnWindowLoad() {
    // Start DrawStuff and load the shader (it's defined at the bottom of this file)
    var ds = IDrawStuff.StartDrawing(window);
    var shader = ds.LoadShader(BasicShader.Config);

    // Create a triangle array with a single square
    var builder = shader.CreateGeometry();
    builder.AddQuad(new(0, 0), new(0, size), new(size, size), new(size, 0));
    var triangles = shader.LoadGeometry(builder);

    // Create a camera that uses pixel coordinates with the origin in the top left
    var screenSize = new Vector2(window.Size.X, window.Size.Y);
    var camera =
        Matrix4x4.CreateScale(2f / screenSize.X, -2f / screenSize.Y, 1f)
        * Matrix4x4.CreateTranslation(-1f, 1f, 0f);

    float time = 0;
    void OnRender(double seconds) {
        // Clear the screen
        ds.ClearWindow();

        // Make the square move in a circle as time passes
        time += (float)seconds;
        var pos = new Vector2(MathF.Cos(time * 2), MathF.Sin(time * 2)) * 300f;
        pos += (screenSize - new Vector2(size, size)) / 2f;
        var transform = Matrix4x4.CreateTranslation(pos.X, pos.Y, 0);

        // Draw the square
        shader.Draw(triangles, transform * camera);
    }

    window.Render += OnRender;
}

window.Load += OnWindowLoad;
window.Run();

[ShaderProgram]
partial class BasicShader {
    Mat4 transform;

    public Vec4 Vertex(Vec2 pos) =>
        transform * vec4(pos, 0f, 1f);

    public RGBA Fragment() =>
        rgba(1, 1, 1, 1);
}

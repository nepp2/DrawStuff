using Silk.NET.Windowing;
using DrawStuff;
using static DrawStuff.ShaderLanguage;
using System.Numerics;
using DotnetNoise;
using Silk.NET.Input;
using System.Drawing;

namespace BlockWorld;

using CubeVert = CubeShader.VertexData;

public class BlockWorldSetup : GameSetup {

    public IWindow CreateWindow() {
        var window = Window.Create(WindowOptions.Default with {
            Title = "Block World",
            Size = new(1500, 1500),
            VSync = true,
        });
        return window;
    }

    public GameLoopFunctions Setup(IWindow window) {

        // Start DrawStuff and load the shader (it's defined at the bottom of this file)
        var ds = IDrawStuff.StartDrawing(window);
        var shader = ds.LoadShader(CubeShader.Config);

        // Load a texture atlas
        var atlas = BlockAtlas.Load(ds);

        // Generate some cube chunks
        var noise = new FastNoise(1342) { UsedNoiseType = FastNoise.NoiseType.Perlin };
        var world = new Geometry<CubeVert>();
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

        float time = 1;
        float zoom = 0;
        float zoomSpeed = 2;
        float speed = 30;
        float angle = 5 * (MathF.PI / 4f);
        Vector3 camPos = Vector3.One * -(chunkSize / 2f + 0.5f);

        void OnRender(double seconds) {
            // Clear the screen
            ds.ClearWindow(Color.LightSkyBlue);

            var screenSize = window.FramebufferSize;

            float baseScale = 0.1f * MathF.Pow(2, zoom);
            float xScale = (worldPixelScale / screenSize.X) * baseScale * 0.9f;
            float yScale = (worldPixelScale / screenSize.Y) * baseScale;

            var transform =
                Matrix4x4.CreateTranslation(camPos)
                * Matrix4x4.CreateFromQuaternion(
                    Quaternion.CreateFromYawPitchRoll(angle, 0, 0))
                * Matrix4x4.CreateFromQuaternion(
                    Quaternion.CreateFromYawPitchRoll(0, -0.5f, 0))
                * Matrix4x4.CreateScale(new Vector3(xScale, yScale, baseScale));

            // Draw the cubes
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
            time += (float)seconds * 0.2f;
            var zoomedSpeed = speed / MathF.Pow(2, zoom);
            if (keyboard.IsKeyPressed(Key.Left))
                angle += (float)seconds;
            if (keyboard.IsKeyPressed(Key.Right))
                angle -= (float)seconds;
            if (keyboard.IsKeyPressed(Key.Up))
                camPos -= new Vector3(MathF.Sin(-angle), 0, MathF.Cos(-angle)) * (float)seconds * zoomedSpeed;
            if (keyboard.IsKeyPressed(Key.Down))
                camPos += new Vector3(MathF.Sin(-angle), 0, MathF.Cos(-angle)) * (float)seconds * zoomedSpeed;
            if (keyboard.IsKeyPressed(Key.W))
                zoom += zoomSpeed * (float)seconds;
            if (keyboard.IsKeyPressed(Key.S))
                zoom -= zoomSpeed * (float)seconds;
            if (keyboard.IsKeyPressed(Key.A))
                camPos += new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)) * (float)seconds * zoomedSpeed;
            if (keyboard.IsKeyPressed(Key.D))
                camPos -= new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)) * (float)seconds * zoomedSpeed;
        };

        return new(OnUpdate, OnRender);
    }
}

// The shader used to draw the cube landscape
[ShaderProgram]
partial class CubeShader {
    Mat4 transform;
    Vec3 lightDir;
    Texture2D tex;

    public record struct FragInput(Vec4 Pos, Vec3 Normal, Vec2 Tc);

    public FragInput Vertex(Vec3 pos, Vec3 normal, Vec2 tc) {
        var tpos = transform * vec4(pos, 1f);
        return new(vec4(tpos.xy, tpos.z * 0.1f, 1), normal, tc);
    }

    public RGBA Fragment(FragInput v) {
        var cosAngle = dot(normalize(v.Normal), normalize(lightDir));
        float facingLight = max(0, cosAngle) * 0.5f;
        float distanceLight = (1f - (0.4f + v.Pos.z * 5)) * 0.25f;
        float ambientLight = 0.25f;
        RGBA colour = sample(tex, v.Tc);
        return vec4(colour.rgb * (facingLight + distanceLight + ambientLight), 1);
    }
}

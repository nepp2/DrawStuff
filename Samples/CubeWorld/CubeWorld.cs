using Silk.NET.Windowing;
using DrawStuff;
using static DrawStuff.ShaderLanguage;
using System.Numerics;

// Create a window
var window = Window.Create(WindowOptions.Default with {
    Title = "DrawSquare",
    Size = new(1000, 1000),
    VSync = true,
});

void GenerateChunk(Geometry<Vector3> world, Random rand, Geometry<Vector3> cube, Vector3 offset) {
    int chunkSize = 16;

    for (int x = 0; x < chunkSize; ++x) {
        for (int y = 0; y < chunkSize; ++y) {
            for (int z = 0; z < chunkSize; ++z) {
                if (rand.NextDouble() > 0.66) {
                    world.PushMap(cube, v => v + new Vector3(x, y, z) + offset);
                }
            }
        }
    }
}

void OnWindowLoad() {
    // Start DrawStuff and load the shader (it's defined at the bottom of this file)
    var ds = IDrawStuff.StartDrawing(window);
    var shader = ds.LoadShader(CubeShader.Config);

    // Create a triangle array with a single square
    var cube = new Geometry<Vector3>().AddCube();

    Random rand = new();
    var world = new Geometry<Vector3>();
    int chunkSize = 16;
    for(int x = -5; x < 5; ++x)
        for (int z = -5; z < 5; ++z)
            GenerateChunk(world, rand, cube, new(x * 16, 0, z * 16));

    var triangles = shader.LoadGeometry(world.WithNormals());

    // Create a camera that uses pixel coordinates with the origin in the top left
    var screenSize = new Vector2(window.Size.X, window.Size.Y);

    // Some text
    var fontShader = ds.LoadShader(SpriteShader.Config);
    var font = ds.LoadDefaultFont(92);

    // Add some geometry for the characters
    var textQuads = fontShader.CreateGeometry();
    Font.DrawText(textQuads, new(0, 0), font, "CUBE WORLD");
    var textGeometry = fontShader.LoadGeometry(textQuads);
    var textSize = new Vector2(
        textQuads.Verts.Select(v => v.pos.x).Max(),
        textQuads.Verts.Select(v => v.pos.y).Max());

    float time = 0;
    void OnRender(double seconds) {
        // Clear the screen
        ds.ClearWindow();

        time += (float)seconds * 0.2f;
        var transform =
            Matrix4x4.CreateTranslation(Vector3.One * -(chunkSize / 2f + 0.5f))
            * Matrix4x4.CreateFromQuaternion(
                Quaternion.CreateFromYawPitchRoll(time, 0, 0))
            * Matrix4x4.CreateFromQuaternion(
                Quaternion.CreateFromYawPitchRoll(0, -0.5f, 0))
            * Matrix4x4.CreateScale(0.05f);

        // Draw the cubes
        shader.Draw(triangles, new(transform, new(-1, -1, -1)));

        var textTransform =
            Matrix4x4.CreateTranslation(new Vector3(
                (window.FramebufferSize.X / 2) - (textSize.X / 2),
                (window.FramebufferSize.Y / 2) - (textSize.Y / 2), 0))
            * ds.GetPixelCamera();

        ds.ClearDepth();
        fontShader.Draw(textGeometry, new(textTransform, font.Texture));
    }

    window.Render += OnRender;
}

window.Load += OnWindowLoad;
window.Run();

[ShaderProgram]
partial class CubeShader {
    Mat4 transform;
    Vec3 lightDir;

    public record struct FragInput(Vec4 Pos, Vec3 Normal);

    public FragInput Vertex(Vec3 pos, Vec3 normal) {
        //var norm = ((transform * vec4(normal, 1)) - (transform * vec4(0, 0, 0, 1))).xyz;
        var tpos = transform * vec4(pos, 1f);
        return new(vec4(tpos.xy, tpos.z * 0.1f, 1), normal);
    }

    public RGBA Fragment(FragInput v) {
        var cosAngle = dot(normalize(v.Normal), normalize(lightDir));
        float facingRatio = max(0, cosAngle) * 0.5f;
        float ambient = (1f - (0.4f + v.Pos.z * 5)) * 0.5f;
        return vec4(vec3(1, 1, 1) * (facingRatio + ambient), 1);
    }
}

public static class GeometryExtensions {
    public static Geometry<Vector3> AddCube(this Geometry<Vector3> g) {
        g.PushQuad(new(0, 0, 1), new(0, 1, 1), new(1, 1, 1), new(1, 0, 1));
        g.PushQuad(new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(0, 0, 0));
        g.PushQuad(new(0, 0, 0), new(0, 0, 1), new(1, 0, 1), new(1, 0, 0));
        g.PushQuad(new(1, 1, 0), new(1, 1, 1), new(0, 1, 1), new(0, 1, 0));
        g.PushQuad(new(1, 0, 0), new(1, 0, 1), new(1, 1, 1), new(1, 1, 0));
        g.PushQuad(new(0, 1, 0), new(0, 1, 1), new(0, 0, 1), new(0, 0, 0));
        return g;
    }

    public static Geometry<Vector3> WithNormalArrows(this Geometry<Vector3> sb) {
        var verts = sb.Verts.AsReadOnlySpan();
        foreach (var t in sb.Triangles.AsReadOnlySpan()) {
            var (a, b, c) = (verts[(int)t.A], verts[(int)t.B], verts[(int)t.C]);
            var normal = Vector3.Cross(a - b, a - c);
            normal /= normal.Length();
            var centre = (a + b + c) / 3f;
            var tip = centre + normal * 5;
            var off = (c - a) / 4;
            sb.PushTriangle(centre - off, centre + off, tip);
            sb.PushTriangle(tip, centre + off, centre - off);
        }
        return sb;
    }

    public static Geometry<CubeShader.VertexData> WithNormals(this Geometry<Vector3> b) {
        var bn = new Geometry<CubeShader.VertexData>();
        var verts = b.Verts.AsReadOnlySpan();
        foreach (var t in b.Triangles.AsReadOnlySpan()) {
            var normal = Vector3.Cross(
                verts[(int)t.B] - verts[(int)t.A],
                verts[(int)t.C] - verts[(int)t.A]);
            bn.PushTriangle(
                new(verts[(int)t.A], normal),
                new(verts[(int)t.B], normal),
                new(verts[(int)t.C], normal));
        }
        return bn;
    }
}
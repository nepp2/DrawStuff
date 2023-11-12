using Silk.NET.Windowing;
using DrawStuff;
using static DrawStuff.ShaderLanguage;
using System.Numerics;

using CubeVert = CubeShader.VertexData;
using DotnetNoise;

// Create a window
var window = Window.Create(WindowOptions.Default with {
    Title = "Cube World",
    Size = new(1000, 1000),
    VSync = true,
});

void OnWindowLoad() {
    // Start DrawStuff and load the shader (it's defined at the bottom of this file)
    var ds = IDrawStuff.StartDrawing(window);
    var shader = ds.LoadShader(CubeShader.Config);

    // Load a texture atlas
    var atlas = CubeAtlas.Load(ds);

    // Generate some cube chunks
    var cubes = CubeModels.Create(atlas);
    var noise = new FastNoise(1342) { UsedNoiseType = FastNoise.NoiseType.Perlin };
    var world = new Geometry<CubeVert>();
    int chunkSize = 16;
    for(int x = -5; x < 5; ++x)
        for (int z = -5; z < 5; ++z)
            world.GenerateChunk(cubes, noise, new(x * 16, 0, z * 16));

    // Load to GPU
    var gpuWorld = shader.LoadGeometry(world);

    // Create a camera that uses pixel coordinates with the origin in the top left
    var screenSize = new Vector2(window.Size.X, window.Size.Y);

    // Some text
    var fontShader = ds.LoadShader(SpriteShader.Config);
    var font = ds.LoadDefaultFont(92);

    // Add some geometry for the characters
    var textQuads = fontShader.CreateGeometry();
    textQuads.AddText(new(0, 0), font, "CUBE WORLD");
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
            * Matrix4x4.CreateScale(0.1f);

        // Draw the cubes
        shader.Draw(gpuWorld, new(transform, new(-1, -1, -1), atlas.Tex));

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
        var colour = sample(tex, v.Tc);
        return vec4(colour.rgb * (facingLight + distanceLight + ambientLight), 1);
    }
}

// Cube face texture atlas
public record CubeAtlas(GPUTexture Tex, TCQuad GrassTop, TCQuad GrassSide, TCQuad Dirt) {
    public static CubeAtlas Load(IDrawStuff ds) {
        var atlas = ds.LoadTexture("../../../Textures-16.png");
        return new(ds.LoadGPUTexture(atlas),
            atlas.GetSubtexture(16 * 3, 16 * 16, 16, 16),
            atlas.GetSubtexture(16 * 3, 16 * 29, 16, 16),
            atlas.GetSubtexture(16 * 3, 16 * 30, 16, 16));
    }
}

// Models generated for each cube type
public record CubeModels(Geometry<CubeVert> Grass, Geometry<CubeVert> Dirt) {
    public static CubeModels Create(CubeAtlas atlas) {
        var grass = new Geometry<CubeVert>().AddCube(atlas.GrassTop, atlas.GrassSide, atlas.Dirt);
        var dirt = new Geometry<CubeVert>().AddCube(atlas.Dirt, atlas.Dirt, atlas.Dirt);
        return new(grass, dirt);
    }
}

public static class GeometryExtensions {


    static void AddCubeFace(
        this Geometry<CubeVert> g,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, TCQuad tp)
    {
        var norm = Vector3.Cross(b - a, c - a);
        g.AddQuad(
            new (a, norm, tp.A),
            new (b, norm, tp.B),
            new (c, norm, tp.C),
            new (d, norm, tp.D));
    }

    public static Geometry<CubeVert> AddCube(
        this Geometry<CubeVert> g, TCQuad top, TCQuad side, TCQuad bottom)
    {
        g.AddCubeFace(new(1, 1, 0), new(1, 1, 1), new(0, 1, 1), new(0, 1, 0), top);
        g.AddCubeFace(new(0, 0, 0), new(0, 0, 1), new(1, 0, 1), new(1, 0, 0), bottom);
        g.AddCubeFace(new(0, 1, 1), new(1, 1, 1), new(1, 0, 1), new(0, 0, 1), side);
        g.AddCubeFace(new(1, 1, 0), new(0, 1, 0), new(0, 0, 0), new(1, 0, 0), side);
        g.AddCubeFace(new(1, 1, 1), new(1, 1, 0), new(1, 0, 0), new(1, 0, 1), side);
        g.AddCubeFace(new(0, 1, 0), new(0, 1, 1), new(0, 0, 1), new(0, 0, 0), side);
        return g;
    }

    public static Geometry<Vector3> AddNormalArrows(this Geometry<Vector3> sb) {
        var verts = sb.Verts.AsReadOnlySpan();
        foreach (var t in sb.Triangles.AsReadOnlySpan()) {
            var (a, b, c) = (verts[(int)t.A], verts[(int)t.B], verts[(int)t.C]);
            var normal = Vector3.Cross(a - b, a - c);
            normal /= normal.Length();
            var centre = (a + b + c) / 3f;
            var tip = centre + normal * 5;
            var off = (c - a) / 4;
            sb.AddTriangle(centre - off, centre + off, tip);
            sb.AddTriangle(tip, centre + off, centre - off);
        }
        return sb;
    }

    public static float Noise(FastNoise noise, Vector2 pos) {
        return (1 + noise.GetNoise(pos.X, pos.Y)) * 0.5f;
    }

    public static float LandscapeNoise(FastNoise n, float x, float y) {
        var pos = new Vector2(x, y);
        var ridges = Noise(n, (Vector2.One * 10000f) - (pos * 5));
        ridges = (int)(ridges * 4);
        ridges /= 4f;
        var detail = Noise(n, pos * 10);
        return ridges * 0.5f + detail * 0.5f;
    }

    public static bool IsGround(FastNoise noise, Vector3 pos) =>
        pos.Y <= LandscapeNoise(noise, pos.X, pos.Z) * 17;

    public static void GenerateChunk(
        this Geometry<CubeVert> world,
        CubeModels cubes,
        FastNoise noise, Vector3 offset)
    {
        int chunkSize = 16;
        for (int x = 0; x < chunkSize; ++x) {
            for (int y = 0; y < chunkSize; ++y) {
                for (int z = 0; z < chunkSize; ++z) {
                    var pos = new Vector3(x, y, z) + offset;
                    if (IsGround(noise, pos)) {
                        var cube = IsGround(noise, pos + Vector3.UnitY)
                            ? cubes.Dirt : cubes.Grass;
                        world.Append(cube, v => {
                            v.pos = v.pos.Cpu() + pos;
                            return v;
                        });
                    }
                }
            }
        }
    }
}
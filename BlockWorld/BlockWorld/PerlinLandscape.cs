
using DotnetNoise;
using DrawStuff;
using System.Diagnostics;
using System.Numerics;

namespace BlockWorld;

public record struct BlockTextures(TCQuad Top, TCQuad Side, TCQuad Bottom);

// Block face texture atlas
public record BlockAtlas(GPUTexture Tex, TCQuad GrassTop, TCQuad GrassSide, TCQuad Dirt) {
    public static BlockAtlas Load(IDrawStuff ds) {
        var atlas = ds.LoadTexture("../../../../Assets/Textures-16.png");
        return new(ds.LoadGPUTexture(atlas),
            atlas.GetSubtexture(16 * 3, 16 * 16, 16, 16),
            atlas.GetSubtexture(16 * 3, 16 * 29, 16, 16),
            atlas.GetSubtexture(16 * 3, 16 * 30, 16, 16));
    }

    public BlockTextures GrassBlock = new(GrassTop, GrassSide, Dirt);
    public BlockTextures DirtBlock = new(Dirt, Dirt, Dirt);
}

public static class GeometryExtensions {

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

    public static void AddBlockQuad(
        this Geometry<BlockVertex> g, Vector3 pos,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, TCQuad tp)
    {
        var norm = Vector3.Cross(b - a, c - a);
        g.AddQuad(
            new(pos + a, norm, tp.A),
            new(pos + b, norm, tp.B),
            new(pos + c, norm, tp.C),
            new(pos + d, norm, tp.D));
    }

    public static Geometry<BlockVertex> AddBlockFaces(
        this Geometry<BlockVertex> g,
        Chunk chunk,
        in BlockTextures tex,
        in Vector3 offset,
        int x, int y, int z)
    {
        var pos = offset + new Vector3(x, y, z);
        if (!chunk.Get(x, y + 1, z))
            g.AddBlockQuad(pos, new(1, 1, 0), new(1, 1, 1), new(0, 1, 1), new(0, 1, 0), tex.Top);
        if (!chunk.Get(x, y - 1, z))
            g.AddBlockQuad(pos, new(0, 0, 0), new(0, 0, 1), new(1, 0, 1), new(1, 0, 0), tex.Bottom);
        if (!chunk.Get(x, y, z + 1))
            g.AddBlockQuad(pos, new(0, 1, 1), new(1, 1, 1), new(1, 0, 1), new(0, 0, 1), tex.Side);
        if (!chunk.Get(x, y, z - 1))
            g.AddBlockQuad(pos, new(1, 1, 0), new(0, 1, 0), new(0, 0, 0), new(1, 0, 0), tex.Side);
        if (!chunk.Get(x + 1, y, z))
            g.AddBlockQuad(pos, new(1, 1, 1), new(1, 1, 0), new(1, 0, 0), new(1, 0, 1), tex.Side);
        if (!chunk.Get(x - 1, y, z))
            g.AddBlockQuad(pos, new(0, 1, 0), new(0, 1, 1), new(0, 0, 1), new(0, 0, 0), tex.Side);
        return g;
    }

    public static void GenerateBlock(
        this Geometry<BlockVertex> world,
        BlockAtlas atlas, Chunk chunk, in Vector3 offset,
        int x, int y, int z) {
        if (chunk.Get(x, y, z)) {
            if (chunk.Get(x, y + 1, z)) {
                world.AddBlockFaces(chunk, atlas.DirtBlock, offset, x, y, z);
            }
            else {
                world.AddBlockFaces(chunk, atlas.GrassBlock, offset, x, y, z);
            }
        }
    }

    public static void GenerateChunkGeometry(
        this Geometry<BlockVertex> world,
        BlockAtlas atlas,
        Chunk chunk, Vector3 offset) {
        for (int x = 0; x < Chunk.Size; ++x)
            for (int y = 0; y < Chunk.Size; ++y)
                for (int z = 0; z < Chunk.Size; ++z)
                    world.GenerateBlock(atlas, chunk, offset, x, y, z);
    }
}

public struct BitArray {
    private uint[] Bits;
    public int Length { get; }

    public BitArray(int length) {
        Length = length;
        int intLen = length / 32;
        if (length % 32 != 0)
            intLen += 1;
        Bits = new uint[intLen];
    }

    public bool this[int i] {
        get => (Bits[i / 32] & (1u << (i % 32))) != 0;
        set {
            if (value)
                Bits[i / 32] |= 1u << (i % 32);
            else
                Bits[i / 32] &= ~(1u << (i % 32));
        }
    }
}

public struct Chunk {
    public const int Size = 16;
    private const int PaddedSize = 18;
    public BitArray Flags;

    public Chunk() {
        Flags = new (PaddedSize * PaddedSize * PaddedSize);
    }

    public bool Get(Vector3 v) => Get((int)v.X, (int)v.Y, (int)v.Z);

    public bool Get(int x, int y, int z) {
        Debug.Assert(x >= -1 && x <= 16);
        Debug.Assert(y >= -1 && y <= 16);
        Debug.Assert(z >= -1 && z <= 16);
        int index = (x + 1) * PaddedSize * PaddedSize + (y + 1) * PaddedSize + (z + 1);
        return Flags[index];
    }

    static float Noise(FastNoise noise, Vector2 pos) {
        return (1 + noise.GetNoise(pos.X, pos.Y)) * 0.5f;
    }

    static float LandscapeNoise(FastNoise n, float x, float y) {
        var pos = new Vector2(x, y);
        var ridges = Noise(n, (Vector2.One * 10000f) - (pos * 5));
        ridges = (int)(ridges * 4);
        ridges /= 4f;
        var detail = Noise(n, pos * 10);
        return ridges * 0.5f + detail * 0.5f;
    }

    static bool IsGround(FastNoise noise, float x, float y, float z) =>
        y <= LandscapeNoise(noise, x, z) * 17;

    public static Chunk Generate(FastNoise noise, Vector3 offset) {
        var chunk = new Chunk();
        int i = 0;
        for (int x = -1; x <= Size; ++x) {
            for (int y = -1; y <= Size; ++y) {
                for (int z = -1; z <= Size; ++z) {
                    chunk.Flags[i] = IsGround(noise, offset.X + x, offset.Y + y, offset.Z + z);
                    ++i;
                }
            }
        }
        return chunk;
    }
}

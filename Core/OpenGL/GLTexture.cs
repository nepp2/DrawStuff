using Silk.NET.OpenGL;

namespace DrawStuff.OpenGL;

public class GLTexture : GPUTexture, IDisposable {

    private GL gl;
    private uint Handle { get; }
    public int Width { get; }
    public int Height { get; }

    private GLTexture(GL gl, uint handle, int width, int height) {
        this.gl = gl;
        Handle = handle;
        Width = width;
        Height = height;
    }

    private static unsafe void WriteTextureData(GL gl, ReadOnlySpan<byte> data, int width, int height) {
        fixed (byte* ptr = data) {
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width,
                (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }
    }

    public static GLTexture Create(GL gl, ReadOnlySpan<byte> data, int width, int height) {
        var texture = gl.GenTexture();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, texture);
        WriteTextureData(gl, data, width, height);

        // Configure wrap behaviour
        gl.TextureParameter(texture, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        gl.TextureParameter(texture, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

        // Configure shrinking and magnification behaviour
        gl.TextureParameter(texture, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        gl.TextureParameter(texture, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

        // Configure mipmaps
        gl.GenerateMipmap(TextureTarget.Texture2D);

        // Unbind
        gl.BindTexture(TextureTarget.Texture2D, 0);
        return new(gl, texture, width, height);
    }

    public void Bind(TextureUnit textureUnit) {
        gl.ActiveTexture(textureUnit);
        gl.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Dispose() {
        gl.DeleteTexture(Handle);
    }
}

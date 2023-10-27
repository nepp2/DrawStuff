using Silk.NET.OpenGL;

namespace DrawStuff;

public class GLTexture : IDisposable {

    private GL gl;
    public uint Handle {  get; }
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
            // Upload our texture data to the GPU.
            // Let's go over each parameter used here:
            // 1. Tell OpenGL that we want to upload to the texture bound in the Texture2D target.
            // 2. We are uploading the "base" texture level, therefore this value should be 0. You don't need to
            //    worry about texture levels for now.
            // 3. We tell OpenGL that we want the GPU to store this data as RGBA formatted data on the GPU itself.
            // 4. The image's width.
            // 5. The image's height.
            // 6. This is the image's border. This valu MUST be 0. It is a leftover component from legacy OpenGL, and
            //    it serves no purpose.
            // 7. Our image data is formatted as RGBA data, therefore we must tell OpenGL we are uploading RGBA data.
            // 8. StbImageSharp returns this data as a byte[] array, therefore we must tell OpenGL we are uploading
            //    data in the unsigned byte format.
            // 9. The actual pointer to our data!
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width,
                (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }
    }

    public static GLTexture Create(GL gl, ReadOnlySpan<byte> data, int width, int height) {
        var texture = gl.GenTexture();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, texture);
        WriteTextureData(gl, data, width, height);

        gl.TextureParameter(texture, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        gl.TextureParameter(texture, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

        // The min and mag filters define how the texture should be sampled as it resized.
        // The min, or minification filter, is used when the texture is reduced in size.
        // The mag, or magnification filter, is used when the texture is increased in size.
        // We're using bilinear filtering here, as it produces a generally nice result.
        // You can also use nearest (point) filtering, or anisotropic filtering, which is only available on the min
        // filter.
        // You may notice that the min filter defines a "mipmap" filter as well. We'll go over mipmaps below.
        //gl.TextureParameter(texture, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        //gl.TextureParameter(texture, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TextureParameter(texture, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        gl.TextureParameter(texture, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

        // Generate mipmaps for this texture.
        // Note: We MUST do this or the texture will appear as black (this is an option you can change but this is
        // out of scope for this tutorial).
        // What is a mipmap?
        // A mipmap is essentially a smaller version of the existing texture. When generating mipmaps, the texture
        // size is continuously halved, generally stopping once it reaches a size of 1x1 pixels. (Note: there are
        // exceptions to this, for example if the GPU reaches its maximum level of mipmaps, which is both a hardware
        // limitation, and a user defined value. You don't need to worry about this for now, so just assume that
        // the mips will be generated all the way down to 1x1 pixels).
        // Mipmaps are used when the texture is reduced in size, to produce a much nicer result, and to reduce moire
        // effect patterns.
        gl.GenerateMipmap(TextureTarget.Texture2D);

        // Unbind the texture as we no longer need to update it any further.
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

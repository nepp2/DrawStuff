
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DrawStuff.OpenGL;

public class GLShader<Vertex, Vars> : Shader<Vertex, Vars>
    where Vertex : unmanaged {
    private GLDrawStuff draw;
    private GLShaderHandle handle;
    private ShaderConfig<Vertex, Vars> config;
    private int[] uniformLocations;

    public GLShader(GLDrawStuff draw, ShaderConfig<Vertex, Vars> config) {
        this.draw = draw;
        this.config = config;
        handle = GLShaderHandle.Compile(draw.GetGL(), config.VertexSrc, config.FragmentSrc);
        uniformLocations = config.Vars.Select(handle.GetUniformLocation).ToArray();
    }

    public GPUGeometry<Vertex, Triangle> CreateGPUGeometry()
        => new GLGeometry<Vertex, Triangle>(draw, config.VertexAttribs);

    public void Draw<ShapeType>(in GPUGeometry<Vertex, ShapeType> shapes, in Vars vars)
        where ShapeType : unmanaged {
        var gl = draw.GetGL();
        gl.Enable(EnableCap.CullFace);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthRange(-100000, 100000);
        gl.Viewport(draw.Window.FramebufferSize);
        handle.Bind();
        config.SetVars(handle, uniformLocations, vars);
        var glShapes = (GLGeometry<Vertex, ShapeType>)shapes;
        var vertexArray = glShapes.VertexArray;
        int indicesPerShape = Marshal.SizeOf<ShapeType>() / sizeof(uint);
        vertexArray.Draw(vertexArray.Ebo.Count * indicesPerShape);
    }

    public void Dispose() {
        handle.Dispose();
    }
}

public struct GLShaderHandle : IDisposable {

    private GL gl;
    public uint Handle { get; }

    private GLShaderHandle(GL gl, uint handle) {
        this.gl = gl;
        Handle = handle;
    }

    public static GLShaderHandle Compile(GL gl, string vertexSrc, string fragmentSrc) {

        //Creating a vertex shader.
        uint vertexShader = gl.CreateShader(ShaderType.VertexShader);
        gl.ShaderSource(vertexShader, vertexSrc);
        gl.CompileShader(vertexShader);

        //Checking the shader for compilation errors.
        string infoLog = gl.GetShaderInfoLog(vertexShader);
        if (!string.IsNullOrWhiteSpace(infoLog)) {
            throw new Exception($"Error compiling vertex shader {infoLog}");
        }

        //Creating a fragment shader.
        uint fragmentShader = gl.CreateShader(ShaderType.FragmentShader);
        gl.ShaderSource(fragmentShader, fragmentSrc);
        gl.CompileShader(fragmentShader);

        //Checking the shader for compilation errors.
        infoLog = gl.GetShaderInfoLog(fragmentShader);
        if (!string.IsNullOrWhiteSpace(infoLog)) {
           throw new Exception($"Error compiling fragment shader {infoLog}");
        }

        //Combining the shaders under one shader program.
        uint shader = gl.CreateProgram();
        gl.AttachShader(shader, vertexShader);
        gl.AttachShader(shader, fragmentShader);
        gl.LinkProgram(shader);

        //Checking the linking for errors.
        gl.GetProgram(shader, GLEnum.LinkStatus, out var status);
        if (status == 0) {
            throw new Exception($"Error linking shader {gl.GetProgramInfoLog(shader)}");
        }

        //Delete the no longer useful individual shaders;
        gl.DetachShader(shader, vertexShader);
        gl.DetachShader(shader, fragmentShader);
        gl.DeleteShader(vertexShader);
        gl.DeleteShader(fragmentShader);

        return new(gl, shader);
    }

    public void Bind() {
        gl.UseProgram(Handle);
    }

    public int GetUniformLocation(string name) {
        int location = gl.GetUniformLocation(Handle, name);
        if (location == -1) {
            throw new UniformNotFoundException(name);
        }
        return location;
    }

    public void SetUniform(int location, TextureUnit slot, GPUTexture texture) =>
        SetUniform(location, slot, (GLTexture)texture);

    public void SetUniform(int location, TextureUnit slot, GLTexture texture) {
        texture.Bind(slot);
        gl.Uniform1(location, (int)slot - (int)TextureUnit.Texture0);
    }

    public unsafe void SetUniform(int location, ShaderLanguage.Mat4 value) =>
        SetUniformMatrix(location, (float*)&value);

    public unsafe void SetUniform(int location, ShaderLanguage.Vec3 v) {
        var vec = new Vector3(v.x, v.y, v.z);
        gl.Uniform3(location, vec);
    }

    public unsafe void SetUniform(string name, Matrix4X4<float> value) =>
        SetUniformMatrix(GetUniformLocation(name), (float*)&value);

    public unsafe void SetUniform(int location, Matrix4X4<float> value) =>
        SetUniformMatrix(location, (float*)&value);

    private unsafe void SetUniformMatrix(int location, float* value) {
        gl.UniformMatrix4(location, 1, false, value);
    }

    public unsafe void BindStorageBuffer<T>(int index, GLBufferObject<T> buffer)
        where T : unmanaged
    {
        gl.BindBufferBase(GLEnum.ShaderStorageBuffer, (uint)index, buffer.Handle);
    }

    public void Dispose() {
        gl.DeleteProgram(Handle);
    }
}

public class UniformNotFoundException : Exception {
    public UniformNotFoundException(string name) : base($"Uniform '{name}' was not found") { }
}

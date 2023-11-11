
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Text.RegularExpressions;

namespace DrawStuff;

public enum UniformType {
    Float,
    UInt32,
    Vec2,
    Vec3,
    Vec4,
    Matrix4x4
}

public record struct GLUniform(UniformType Type, string Name);

public record ShaderInputs(GLAttribute[] Attributes);

public class ShaderInfo {

    record struct AttribDecl(string Type, string Name, int Location);

    static Regex attributeRegex = new(
        @"layout\s*\(\s*location\s*=\s*(?<location>\d+)\s*\)\s*in\s*(?<type>\w+)\s*(?<name>\w+)\s*;",
        RegexOptions.Compiled);

    public static ShaderInputs Infer(string vertShaderSrc, string fragShaderSrc) {
        var attributes = attributeRegex.Matches(vertShaderSrc)
            .Select(m => new AttribDecl(
                m.Groups["type"].Value, m.Groups["name"].Value, int.Parse(m.Groups["location"].Value)))
            .OrderBy(d => d.Location)
            .Select(d => d.Type switch {
                "float" => new GLAttribute(d.Name, 1, GLAttribPtrType.Float32),
                "uint" => new GLAttribute(d.Name, 1, GLAttribPtrType.Uint32),
                "vec2" => new GLAttribute(d.Name, 2, GLAttribPtrType.Float32),
                "vec3" => new GLAttribute(d.Name, 3, GLAttribPtrType.Float32),
                "vec4" => new GLAttribute(d.Name, 4, GLAttribPtrType.Float32),
                _ => throw new NotSupportedException($"Unknown shader attribute type {d.Type}"),
            }).ToArray();
        return new(attributes);
    }
}

public class UniformNotFoundException : Exception {
    public UniformNotFoundException(string name) : base($"Uniform '{name}' was not found") { }
}

public class GLShader : IDisposable {

    private GL gl;
    public uint Handle { get; }
    public ShaderInputs Inputs { get; }

    private GLShader(GL gl, uint handle, ShaderInputs inputs) {
        this.gl = gl;
        Handle = handle;
        Inputs = inputs;
    }

    public static GLShader Compile(GL gl, string vertexSrc, string fragmentSrc) {

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

        return new(gl, shader, ShaderInfo.Infer(vertexSrc, fragmentSrc));
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

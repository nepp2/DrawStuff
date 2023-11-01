
namespace DrawStuff;

public delegate void SetShaderVarsFunc<Vars>(GLShader shader, int[] varLocations, in Vars v);

public record ShaderConfig(
    string VertexSrc,
    string FragmentSrc,
    string[] Vars,
    GLAttribute[] VertexAttribs);

public record ShaderConfig<Vertex, Vars>(
    string VertexSrc,
    string FragmentSrc,
    SetShaderVarsFunc<Vars> SetVars,
    string[] Vars,
    GLAttribute[] VertexAttribs)
        : ShaderConfig(VertexSrc, FragmentSrc, Vars, VertexAttribs)
        where Vertex : unmanaged;

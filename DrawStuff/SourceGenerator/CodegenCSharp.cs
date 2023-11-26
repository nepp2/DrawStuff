
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace ShaderCompiler;

public struct SrcNode {
    private object Obj;

    public static implicit operator SrcNode(string s) => new SrcNode() { Obj = s };
    public static implicit operator SrcNode(SrcList t) => new SrcNode() { Obj = t };

    string? TryString() => Obj as string;
    SrcList? TryList() => Obj as SrcList;

    public void WriteTree(StringBuilder sb) {
        WriteFreshLine(sb, 0);
    }

    private void WriteFreshLine(StringBuilder sb, int indent) {
        if (TryString() is string line) {
            for (int i = 0; i < indent; ++i)
                sb.Append("    ");
            sb.AppendLine(line);
        }
        else if (TryList() is SrcList t) {
            if (t.MultiLine) {
                foreach (var n in t.Nodes) {
                    n.WriteFreshLine(sb, indent + t.Indent);
                }
            }
            else {
                for (int i = 0; i < indent; ++i)
                    sb.Append("    ");
                foreach (var n in t.Nodes) {
                    n.AppendCurrentLine(sb, indent);
                }
                sb.AppendLine();
            }
        }
        else {
            throw new ShaderGenException("Unexpected element in source tree");
        }
    }

    private void AppendCurrentLine(StringBuilder sb, int indent) {
        if (TryString() is string line) {
            sb.Append(line);
        }
        else if (TryList() is SrcList t) {
            if (t.MultiLine) {
                sb.AppendLine();
                foreach (var n in t.Nodes) {
                    n.WriteFreshLine(sb, indent + t.Indent);
                }
                for (int i = 0; i < indent; ++i)
                    sb.Append("    ");
            }
            else {
                foreach (var n in t.Nodes) {
                    n.AppendCurrentLine(sb, indent);
                }
            }
        }
        else {
            throw new ShaderGenException("Unexpected element in source tree");
        }
    }
}

public static class EnumerableSeparatorJoin {
    public static IEnumerable<T> WithSeparator<T>(this IEnumerable<T> seq, T sep) {
        bool join = false;
        foreach(var v in seq) {
            if (join) yield return sep;
            else join = true;
            yield return v;
        }
    }
}

public record SrcList(int Indent, bool MultiLine, SrcNode[] Nodes);
public class Src {
    public static SrcNode Root(params SrcNode[] ns) => new SrcList(0, true, ns);
    public static SrcNode Lines(params SrcNode[] ns) => new SrcList(0, true, ns);
    public static SrcNode Expr(params SrcNode[] ns) => new SrcList(0, false, ns);
    public static SrcNode Expr(IEnumerable<SrcNode> ns) => new SrcList(0, false, ns.ToArray());
    public static SrcNode Empty() => Lines();
    public static SrcNode Indent(params SrcNode[] ns) => new SrcList(1, true, ns);
    public static SrcNode Indent(IEnumerable<SrcNode> ns) => new SrcList(1, true, ns.ToArray());
    public static SrcNode Indent(IEnumerable<string> ns) => new SrcList(1, true, ns.Select(s => (SrcNode)s).ToArray());
}


public record CodegenOutput(string CSharpTemplate, string CSharpSrc, string VertexSrc, string FragmentSrc);

public class CodegenCSharp {

    private List<Diagnostic> errors { get; }

    public CodegenCSharp(List<Diagnostic> errors) { this.errors = errors; }

    private static string ToCSharpType(ValueType t) => t switch {
        FloatType => "float",
        Vec2Type => "Vec2",
        Vec3Type => "Vec3",
        Vec4Type => "Vec4",
        UInt32Type => "uint",
        Mat4Type => "Mat4",
        RGBAType => "RGBA",
        TextureType => "GPUTexture",
        CustomStruct cs => cs.Name,
        _ => throw new ShaderGenException("Unknown value type"),
    };

    private void Error(string msg, Location loc) {
        errors.Add(Diagnostic.Create(ShaderDiagnostic.InvalidShader, loc, msg));
    }

    (string, int) ErrorAttrib(string msg, Location loc) {
        Error(msg, loc);
        return ("GLAttribPtrType.Float32", 1);
    }

    private (string PtrType, int NumVals) GetVertexAttribInfo(ValueType type, Location loc) {
        return type switch {
            FloatType => ("GLAttribPtrType.Float32", 1),
            Vec2Type => ("GLAttribPtrType.Float32", 2),
            Vec3Type => ("GLAttribPtrType.Float32", 3),
            Vec4Type or RGBAType =>
                ("GLAttribPtrType.Float32", 4),
            UInt32Type => ("GLAttribPtrType.Uint32", 1),
            Mat4Type => ("GLAttribPtrType.Float32", 16),

            TextureType => ErrorAttrib("Can't pass textures as vertex data", loc),
            CustomStruct => ErrorAttrib("Can't pass custom structs as vertex data", loc),
            _ => ErrorAttrib("Unknown value type", loc),
        };
    }

    private static SrcNode GenerateConstructor(string className, ArgumentInfo[] args) {
        if (args.Any()) {
            string argDefs = string.Join(", ", args.Select(a => $"{ToCSharpType(a.Type)} {a.Name}"));
            return Src.Lines(
                $"public {className}({argDefs}) {{",
                Src.Indent(args.Select(a => $"this.{a.Name} = {a.Name};")),
                @"}"
            );
        }
        return Src.Empty();
    }

    private static SrcNode ToFieldDef(ArgumentInfo arg) =>
        $"public {ToCSharpType(arg.Type)} {arg.Name};";

    private (string vertexType, SrcNode src) GenerateVertexCode(ArgumentInfo[] vertexInputs) {
        var vertexAttribs = Src.Lines(
            @"public static readonly GLAttribute[] VertexAttributes = new GLAttribute[] {",
            Src.Indent(vertexInputs.Select(v => {
                var attrib = GetVertexAttribInfo(v.Type, v.Loc);
                return $"new (\"{v.Name}\", {attrib.NumVals}, {attrib.PtrType}),";
            })),
            @"};",
            @""
        );
        if (vertexInputs.Length == 1) {
            return (ToCSharpType(vertexInputs[0].Type), vertexAttribs);
        }
        else {
            var typeDef = Src.Lines(
                @"[StructLayout(LayoutKind.Sequential)]",
                @"public struct VertexData {",
                Src.Indent(
                    Src.Lines(vertexInputs.Select(ToFieldDef).ToArray()),
                    "",
                    GenerateConstructor("VertexData", vertexInputs)
                ),
                @"}",
                @"",
                vertexAttribs,
                @""
            );
            return ("VertexData", typeDef);
        }
    }

    static SrcNode GenerateSetVarStatement(int index, ref int nextTextureSlot, ValueType type, string value) {
        if (type is TextureType) {
            int slot = nextTextureSlot++;
            return $"shader.SetUniform(varLocations[{index}], TextureUnit.Texture{slot}, {value});";
        }
        else {
            return $"shader.SetUniform(varLocations[{index}], {value});";
        }
    }

    private static (string varType, SrcNode code) GenerateVarsCode(ArgumentInfo[] globals) {
        int nextTextureSlot = 0;
        if (globals.Length == 1) {
            var g = globals[0];
            var varType = ToCSharpType(g.Type);
            var code = Src.Lines(
                $"public static void SetShaderVars(GLShaderHandle shader, int[] varLocations, in {varType} v) {{",
                Src.Indent(GenerateSetVarStatement(0, ref nextTextureSlot, g.Type, "v")),
                @"}",
                @""
            );
            return (varType, code);
        }
        else {
            var code = Src.Lines(
                @"public struct Vars {",
                Src.Indent(
                    Src.Lines(globals.Select(ToFieldDef).ToArray()),
                    "",
                    GenerateConstructor("Vars", globals)
                ),
                @"}",
                @"",
                @"public static void SetShaderVars(GLShaderHandle shader, int[] varLocations, in Vars v) {",
                Src.Indent(globals.Select((g, i) => GenerateSetVarStatement(i, ref nextTextureSlot, g.Type, $"v.{g.Name}"))),
                @"}",
                @""
            );
            return ("Vars", code);
        }
    }

    public static CodegenOutput GenerateClassExtension(List<Diagnostic> errors, ShaderInfo info, SemanticModel model) {
        var cg = new CodegenCSharp(errors);
        return cg.GenerateClassExtension(info, model);
    }

    private CodegenOutput GenerateClassExtension(ShaderInfo info, SemanticModel model) {
        var vertexSrc = CodegenGLSL.Compile(errors, model, CompileMode.VertexEntryPoint, info, info.Vertex);
        var fragmentSrc = CodegenGLSL.Compile(errors, model, CompileMode.FragmentEntryPoint, info, info.Fragment);

        var (vertexType, vertexTypeDef) = GenerateVertexCode(info.Vertex.Inputs);
        var (varsType, varsCode) = GenerateVarsCode(info.Globals);

        var classDef = Src.Lines(
            $"public partial class {info.Sym.Name} {{",
            Src.Indent(
                $"public static string VertexSource = @\"[[VERTEX_SRC]]\";",
                $"public static string FragmentSource = @\"[[FRAGMENT_SRC]]\";",
                @"",
                @"public static readonly string[] VarNames = new string[] {",
                $"    {string.Join(", ", info.Globals.Select(g => $"\"{g.Name}\""))}",
                @"};",
                @"",
                vertexTypeDef,
                varsCode,
                $"public static ShaderConfig<{vertexType}, {varsType}> Config =>",
                @"    new(VertexSource, FragmentSource, SetShaderVars, VarNames, VertexAttributes);"
            ),
            @"}"
        );

        var shaderNamespace = info.Sym.ContainingNamespace;
        if(!shaderNamespace.IsGlobalNamespace) {
            classDef = Src.Lines(
                $"namespace {shaderNamespace.ToDisplayString()} {{",
                Src.Indent(classDef),
                @"}"
            );
        }

        var source = Src.Root(
            "using System.Runtime.InteropServices;",
            "using Silk.NET.OpenGL;",
            "using DrawStuff;",
            "using DrawStuff.OpenGL;",
            "using static DrawStuff.ShaderLanguage;",
            "",
            classDef
        );

        var sb = new StringBuilder();
        source.WriteTree(sb);
        var template = sb.ToString();

        sb.Replace("[[VERTEX_SRC]]", vertexSrc);
        sb.Replace("[[FRAGMENT_SRC]]", fragmentSrc);
        var fullSrc = sb.ToString();

        return new(template, fullSrc, vertexSrc, fragmentSrc);
    }
}

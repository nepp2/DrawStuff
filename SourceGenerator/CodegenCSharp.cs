
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace ShaderCompiler;

struct SourceNode {
    private object Obj;
    public static implicit operator SourceNode(string s) => new SourceNode() { Obj = s };
    public static implicit operator SourceNode(string[] l) => new SourceNode() { Obj = l };
    public static implicit operator SourceNode(SourceTree t) => new SourceNode() { Obj = t };

    public string? TryString() => Obj as string;
    public string[]? TryStringArray() => Obj as string[];
    public SourceTree? TryTree() => Obj as SourceTree;
}

class SourceTree {
    private SourceNode[] Nodes;
    private int Indent = 1;
    public SourceTree(params SourceNode[] nodes) { Nodes = nodes; }
    public SourceTree(IEnumerable<string> nodes) { Nodes = nodes.Select(s => (SourceNode)s).ToArray(); }
    public SourceTree(int indent, params SourceNode[] nodes) { Indent = indent; Nodes = nodes; }

    public void Write(StringBuilder sb, int indent = 0) {
        foreach (var n in Nodes) {
            if (n.TryString() is string line) {
                for (int i = 0; i < indent; ++i) sb.Append("    ");
                sb.AppendLine(line);
            }
            else if (n.TryStringArray() is string[] lines) {
                foreach (var l in lines) {
                    for (int i = 0; i < indent; ++i) sb.Append("    ");
                    sb.AppendLine(l);
                }
            }
            else if (n.TryTree() is SourceTree t) {
                t.Write(sb, indent + t.Indent);
            }
            else {
                throw new ShaderGenException("Unexpected element in source tree");
            }
        }
    }
}

public record CodegenOutput(string CSharpTemplate, string CSharpSrc, string VertexSrc, string FragmentSrc);

public class CodegenCSharp {

    private static string ToCSharpType(ValType t) => t switch {
        ValType.Float => $"float",
        ValType.Vec2 => $"Vec2",
        ValType.Vec3 => $"Vec3",
        ValType.Vec4 => $"Vec4",
        ValType.UInt32 => $"UInt32",
        ValType.Mat4 => $"Mat4",
        ValType.RGBA => $"RGBA",
        ValType.VertexPos => $"VertexPos",
        _ => throw new ShaderGenException("Unknown attribute type"),
    };


    private static string[] GenerateConstructor(string className, IEnumerable<ArgumentInfo> args) {
        var code = new List<string>();
        if (args.Any()) {
            string argDefs = string.Join(", ", args.Select(a => $"{ToCSharpType(a.Type)} {a.Name}"));
            string assignments = string.Join(" ", args.Select(a => $"this.{a.Name} = {a.Name};"));
            code.Add($"public {className}({argDefs}) {{ {assignments} }}");
        }
        if (args.Count() == 1) {
            code.Add($"public static implicit operator {className}({ToCSharpType(args.First().Type)} v) => new(v);");
        }
        return code.ToArray();
    }

    private static string ToFieldDef(ArgumentInfo arg) =>
        $"public {ToCSharpType(arg.Type)} {arg.Name};";

    public static CodegenOutput GenerateClassExtension(ShaderAnalyze info, SemanticModel model) {
        var vertexSrc = CodegenGLSL.Compile(model, CompileMode.VertexEntryPoint, info.Vertex, info.Globals);
        var fragmentSrc = CodegenGLSL.Compile(model, CompileMode.FragmentEntryPoint, info.Fragment, info.Globals);
        var source = new SourceTree(
            $"public static partial class {info.Sym.Name} {{",
            new SourceTree(
                $"public const string VertexSource = @\"[[VERTEX_SRC]]\";",
                $"public const string FragmentSource = @\"[[FRAGMENT_SRC]]\";",
                @"",
                @"[StructLayout(LayoutKind.Sequential)]",
                @"public struct VertexData {",
                new SourceTree(
                    info.Vertex.Inputs.Select(ToFieldDef).ToArray(),
                    "",
                    GenerateConstructor("VertexData", info.Vertex.Inputs)
                ),
                @"}",
                @"",
                @"public struct Vars {",
                new SourceTree(
                    info.Globals.Select(ToFieldDef).ToArray(),
                    "",
                    GenerateConstructor("Vars", info.Globals)
                ),
                @"}",
                @"",
                @"public static void SetShaderVars(GLShader shader, in Vars v) {",
                    new SourceTree(info.Globals.Select(g => $"shader.SetUniform(\"{g.Name}\", v.{g.Name});")),
                @"}",
                @"",
                @"public static RenderConfig<VertexData, Vars> PipelineConfig =>",
                @"    new(VertexSource, FragmentSource, SetShaderVars);"
            ),
            @"}"
        );
        var ns = info.Sym.ContainingNamespace;
        if (!ns.IsGlobalNamespace) {
            source = new SourceTree(
                $"namespace {ns.ToDisplayString()}",
                source,
                @"}"
            );
        }
        var sb = new StringBuilder();
        sb.AppendLine("using System.Runtime.InteropServices;");
        sb.AppendLine("using Silk.NET.OpenGL;");
        sb.AppendLine("using DrawStuff;");
        sb.AppendLine("using static DrawStuff.ShaderLanguage;");
        sb.AppendLine("");
        source.Write(sb);

        var template = sb.ToString();
        sb.Replace("[[VERTEX_SRC]]", vertexSrc);
        sb.Replace("[[FRAGMENT_SRC]]", fragmentSrc);

        return new(template, sb.ToString(), vertexSrc, fragmentSrc);
    }
}

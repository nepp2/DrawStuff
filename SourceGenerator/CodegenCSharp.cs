
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace ShaderCompiler;

struct SourceNode {
    private object Obj;
    public static implicit operator SourceNode(string s) => new SourceNode() { Obj = s };
    public static implicit operator SourceNode(SourceTree t) => new SourceNode() { Obj = t };

    public string? TryString() => Obj as string;
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


    private static string GenerateConstructor(string className, IEnumerable<ArgumentInfo> args) {
        if (args.Any()) {
            string argDefs = string.Join(", ", args.Select(a => $"{ToCSharpType(a.Type)} {a.Name}"));
            string assignments = string.Join(" ", args.Select(a => $"this.{a.Name} = {a.Name};"));
            return $"public {className}({argDefs}) {{ {assignments} }}";
        }
        return "";
    }

    private static string ToFieldDef(ArgumentInfo arg) =>
        $"public {ToCSharpType(arg.Type)} {arg.Name};";

    private static SourceTree VertexInputStruct(ShaderAnalyze info) {
        return new SourceTree(
            @"[StructLayout(LayoutKind.Sequential)]",
            @"public struct VertexInputs {",
            new SourceTree(info.Vertex.Inputs.Select(ToFieldDef)),
            new SourceTree(
                "",
                GenerateConstructor("VertexInputs", info.Vertex.Inputs)),
            @"}"
        );
    }

    private static SourceTree StaticVarsStruct(ShaderAnalyze info) {
        return new SourceTree(
            @"[StructLayout(LayoutKind.Sequential)]",
            @"public struct StaticVars {",
            new SourceTree(info.Globals.Select(ToFieldDef)),
            new SourceTree(
                "",
                GenerateConstructor("StaticVars", info.Globals)),
            @"}"
        );
    }
    public static CodegenOutput GenerateClassExtension(ShaderAnalyze info, SemanticModel model) {
        var vertexSrc = CodegenGLSL.Compile(model, CompileMode.VertexEntryPoint, info.Vertex, info.Globals);
        var fragmentSrc = CodegenGLSL.Compile(model, CompileMode.FragmentEntryPoint, info.Fragment, info.Globals);
        var source = new SourceTree(
            $"public static partial class {info.Sym.Name} {{",
            $"    public const string VertexSource = @\"[[VERTEX_SRC]]\";",
            $"    public const string FragmentSource = @\"[[FRAGMENT_SRC]]\";",
            @"",
            VertexInputStruct(info),
            @"",
            StaticVarsStruct(info),
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
        sb.AppendLine("using static DrawStuff.ShaderLang;");
        sb.AppendLine("");
        source.Write(sb);

        var template = sb.ToString();
        sb.Replace("[[VERTEX_SRC]]", vertexSrc);
        sb.Replace("[[FRAGMENT_SRC]]", fragmentSrc);

        return new(template, sb.ToString(), vertexSrc, fragmentSrc);
    }
}

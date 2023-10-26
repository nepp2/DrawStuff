
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
    private static string ToFieldDef(ArgumentInfo arg) {
        switch (arg.Type) {
            case ValType.Float: return $"public float {arg.Name};";
            case ValType.Vec2: return $"public Vec2 {arg.Name};";
            case ValType.Vec3: return $"public Vec3 {arg.Name};";
            case ValType.Vec4: return $"public Vec4 {arg.Name};";
            case ValType.UInt32: return $"public UInt32 {arg.Name};";
            case ValType.Mat4: return $"public Mat4 {arg.Name};";
            case ValType.RGBA: return $"public RGBA {arg.Name};";
            case ValType.VertexPos: return $"public VertexPos {arg.Name};";
        }
        throw new ShaderGenException("Unknown attribute type");
    }

    private static SourceTree VertexInputStruct(ShaderAnalyze info) {
        return new SourceTree(
            @"[StructLayout(LayoutKind.Sequential)]",
            @"public struct VertexInputs {",
            new SourceTree(info.Vertex.Inputs.Select(ToFieldDef)),
            @"}"
        );
    }

    private static SourceTree StaticVarsStruct(ShaderAnalyze info) {
        return new SourceTree(
            @"[StructLayout(LayoutKind.Sequential)]",
            @"public struct StaticVars {",
            new SourceTree(info.Configs.Select(ToFieldDef)),
            @"}"
        );
    }
    public static CodegenOutput GenerateClassExtension(ShaderAnalyze info, SemanticModel model) {
        var vertexSrc = CodegenGLSL.Compile(model, info.Vertex, CompileMode.VertexEntryPoint);
        var fragmentSrc = CodegenGLSL.Compile(model, info.Fragment, CompileMode.FragmentEntryPoint);
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

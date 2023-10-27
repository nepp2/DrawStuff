
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

    public void Write(StringBuilder sb, int indent = 0) {
        if (TryString() is string line) {
            for (int i = 0; i < indent; ++i) sb.Append("    ");
            sb.AppendLine(line);
        }
        else if (TryList() is SrcList t) {
            t.Write(sb, indent);
        }
        else {
            throw new ShaderGenException("Unexpected element in source tree");
        }
    }
}

public record SrcList(int Indent, SrcNode[] Nodes) {
    public void Write(StringBuilder sb, int indent) {
        foreach (var n in Nodes) {
            n.Write(sb, indent + Indent);
        }
    }
}

public class Src {
    public static SrcNode Root(params SrcNode[] ns) => new SrcList(0, ns);
    public static SrcNode Inline(params SrcNode[] ns) => new SrcList(0, ns);
    public static SrcNode Empty() => Inline();
    public static SrcNode Indent(params SrcNode[] ns) => new SrcList(1, ns);
    public static SrcNode Indent(IEnumerable<SrcNode> ns) => new SrcList(1, ns.ToArray());
    public static SrcNode Indent(IEnumerable<string> ns) => new SrcList(1, ns.Select(s => (SrcNode)s).ToArray());
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
        _ => throw new ShaderGenException("Unknown value type"),
    };

    private static (string PtrType, int NumVals) GetVertexAttribInfo(ValType type) {
        return type switch {
            ValType.Float => ("GLAttribPtrType.Float32", 1),
            ValType.Vec2 => ("GLAttribPtrType.Float32", 2),
            ValType.Vec3 => ("GLAttribPtrType.Float32", 3),
            ValType.Vec4 or ValType.RGBA or ValType.VertexPos =>
                ("GLAttribPtrType.Float32", 4),
            ValType.UInt32 => ("GLAttribPtrType.Uint32", 1),
            ValType.Mat4 => ("GLAttribPtrType.Float32", 16),
            _ => throw new ShaderGenException("Unknown value type"),
        };
    }

    private static SrcNode GenerateConstructor(string className, ArgumentInfo[] args) {
        if (args.Any()) {
            string argDefs = string.Join(", ", args.Select(a => $"{ToCSharpType(a.Type)} {a.Name}"));
            string assignments = string.Join(" ", args.Select(a => $"this.{a.Name} = {a.Name};"));
            return $"public {className}({argDefs}) {{ {assignments} }}";
        }
        return Src.Empty();
    }

    private static SrcNode ToFieldDef(ArgumentInfo arg) =>
        $"public {ToCSharpType(arg.Type)} {arg.Name};";

    private static (string vertexType, SrcNode src) GenerateVertexCode(ArgumentInfo[] vertexInputs) {
        var vertexAttribs = Src.Inline(
            @"public static readonly GLAttribute[] VertexAttributes = new GLAttribute[] {",
            Src.Indent(vertexInputs.Select(v => {
                var attrib = GetVertexAttribInfo(v.Type);
                return $"new (\"{v.Name}\", {attrib.NumVals}, {attrib.PtrType}),";
            })),
            @"};",
            @""
        );
        if (vertexInputs.Length == 1) {
            return (ToCSharpType(vertexInputs[0].Type), vertexAttribs);
        }
        else {
            var typeDef = Src.Inline(
                @"[StructLayout(LayoutKind.Sequential)]",
                @"public struct VertexData {",
                Src.Indent(
                    Src.Inline(vertexInputs.Select(ToFieldDef).ToArray()),
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

    private static (string varType, SrcNode code) GenerateVarsCode(ArgumentInfo[] globals) {
        if (globals.Length == 1) {
            var g = globals[0];
            var varType = ToCSharpType(g.Type);
            var code = Src.Inline(
                $"public static void SetShaderVars(GLShader shader, in {varType} v) {{",
                $"    shader.SetUniform(\"{g.Name}\", v);",
                @"}",
                @""
            );
            return (varType, code);
        }
        else {
            var code = Src.Inline(
                @"public struct Vars {",
                Src.Indent(
                    Src.Inline(globals.Select(ToFieldDef).ToArray()),
                    "",
                    GenerateConstructor("Vars", globals)
                ),
                @"}",
                @"",
                @"public static void SetShaderVars(GLShader shader, in Vars v) {",
                    Src.Indent(globals.Select(g => $"shader.SetUniform(\"{g.Name}\", v.{g.Name});")),
                @"}",
                @""
            );
            return ("Vars", code);
        }
    }

    public static CodegenOutput GenerateClassExtension(ShaderAnalyze info, SemanticModel model) {
        var vertexSrc = CodegenGLSL.Compile(model, CompileMode.VertexEntryPoint, info.Vertex, info.Globals);
        var fragmentSrc = CodegenGLSL.Compile(model, CompileMode.FragmentEntryPoint, info.Fragment, info.Globals);

        var (vertexType, vertexTypeDef) = GenerateVertexCode(info.Vertex.Inputs);
        var (varsType, varsCode) = GenerateVarsCode(info.Globals);

        var shaderNamespace = info.Sym.ContainingNamespace;
        SrcNode namespaceDecl = shaderNamespace.IsGlobalNamespace
            ? Src.Inline()
            : Src.Inline($"namespace {shaderNamespace.ToDisplayString()};", "");

        var source = Src.Root(
            "using System.Runtime.InteropServices;",
            "using Silk.NET.OpenGL;",
            "using DrawStuff;",
            "using static DrawStuff.ShaderLanguage;",
            "",
            namespaceDecl,
            $"public static partial class {info.Sym.Name} {{",
            Src.Indent(
                $"public const string VertexSource = @\"[[VERTEX_SRC]]\";",
                $"public const string FragmentSource = @\"[[FRAGMENT_SRC]]\";",
                @"",
                vertexTypeDef,
                varsCode,
                $"public static ShaderConfig<{vertexType}, {varsType}> Config =>",
                @"    new(VertexSource, FragmentSource, SetShaderVars, VertexAttributes);"
            ),
            @"}"
        );

        var sb = new StringBuilder();
        source.Write(sb);
        var template = sb.ToString();

        sb.Replace("[[VERTEX_SRC]]", vertexSrc);
        sb.Replace("[[FRAGMENT_SRC]]", fragmentSrc);
        var fullSrc = sb.ToString();

        return new(template, fullSrc, vertexSrc, fragmentSrc);
    }
}

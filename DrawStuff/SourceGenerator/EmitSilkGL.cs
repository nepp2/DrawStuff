
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using ShaderCompiler.GL;

namespace ShaderCompiler;

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

public record ShaderResult(string Name, string CSharpTemplate, string CSharpSrc, string VertexSrc, string FragmentSrc);

public class EmitSilkGL {

    private List<Diagnostic> errors { get; }

    public EmitSilkGL(List<Diagnostic> errors) { this.errors = errors; }

    SrcWriter w = new ();

    private static string ToCSharpType(TypeTag t) => t switch {
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

    private (string PtrType, int NumVals) GetVertexAttribInfo(TypeTag type, Location loc) {
        return type switch {
            FloatType => ("GLAttribPtrType.Float32", 1),
            Vec2Type => ("GLAttribPtrType.Float32", 2),
            Vec3Type => ("GLAttribPtrType.Float32", 3),
            Vec4Type or RGBAType => ("GLAttribPtrType.Float32", 4),
            UInt32Type => ("GLAttribPtrType.Uint32", 1),
            Mat4Type => ("GLAttribPtrType.Float32", 16),
            CustomStruct cs => ErrorAttrib("Can't pass nested struct as vertex data", loc),
            TextureType => ErrorAttrib("Can't pass textures as vertex data", loc),
            VoidType => ErrorAttrib("Can't pass void as vertex data", loc),
            _ => ErrorAttrib("Unknown value type", loc),
        };
    }

    private void WriteConstructor(string className, ArgumentInfo[] args) {
        if (args.Any()) {
            string argDefs = string.Join(", ", args.Select(a => $"{ToCSharpType(a.Type)} {a.Name}"));
            w.WriteLine($"public {className}({argDefs}) {{");
            using (w.Indent()) {
                foreach (var a in args)
                    w.WriteLine($"this.{a.Name} = {a.Name};");
            }
            w.WriteLine("}");
        }
    }

    private void WriteFieldDef(ArgumentInfo arg) =>
        w.WriteLine($"public {ToCSharpType(arg.Type)} {arg.Name};");

    private string WriteVertexInputCode(ArgumentInfo[] vertexInputs) {
        w.WriteLine(@"public static GLAttribute[] VertexAttributes() => new GLAttribute[] {");
        using (w.Indent()) {
            foreach(var v in vertexInputs) {
                if(v.Type is CustomStruct cs) {
                    foreach(var f in cs.Fields) {
                        var attrib = GetVertexAttribInfo(f.Type, v.Loc);
                        w.WriteLine($"new (\"{v.Name}__{f.Name}\", {attrib.NumVals}, {attrib.PtrType}),");
                    }
                }
                else {
                    var attrib = GetVertexAttribInfo(v.Type, v.Loc);
                    w.WriteLine($"new (\"{v.Name}\", {attrib.NumVals}, {attrib.PtrType}),");
                }
            }
        }
        w.WriteLine("};");
        w.WriteLine();
        if (vertexInputs.Length == 1) {
            return ToCSharpType(vertexInputs[0].Type);
        }
        else {
            w.WriteLine("[StructLayout(LayoutKind.Sequential)]");
            w.WriteLine("public struct VertexData {");
            using (w.Indent()) {
                foreach (var v in vertexInputs)
                    WriteFieldDef(v);
                w.WriteLine();
                WriteConstructor("VertexData", vertexInputs);
            }
            w.WriteLine("}");
            w.WriteLine();
            return "VertexData";
        }
    }

    private void WriteSetVarStatement(int index, ref int nextTextureSlot, TypeTag type, string value) {
        if (type is TextureType) {
            int slot = nextTextureSlot++;
            w.WriteLine($"shader.SetUniform(varLocations[{index}], TextureUnit.Texture{slot}, {value});");
        }
        else {
            w.WriteLine($"shader.SetUniform(varLocations[{index}], {value});");
        }
    }

    private string WriteVarsCode(ArgumentInfo[] globals) {
        int nextTextureSlot = 0;
        if (globals.Length == 1) {
            var g = globals[0];
            var varType = ToCSharpType(g.Type);
            w.WriteLine($"public static void SetShaderVars(GLShaderHandle shader, int[] varLocations, in {varType} v) {{");
            using (w.Indent())
                WriteSetVarStatement(0, ref nextTextureSlot, g.Type, "v");
            w.WriteLine(@"}");
            w.WriteLine(@"");
            return varType;
        }
        else {
            w.WriteLine(@"public struct Vars {");
            using (w.Indent()) {
                foreach(var g in globals)
                    WriteFieldDef(g);
                w.WriteLine();
                WriteConstructor("Vars", globals);
            }
            w.WriteLine("}");
            w.WriteLine("");
            w.WriteLine(
                "public static void SetShaderVars("
                + "GLShaderHandle shader, int[] varLocations, in Vars v) {");
            using (w.Indent()) {
                foreach (var (g, i) in globals.Indexed())
                    WriteSetVarStatement(i, ref nextTextureSlot, g.Type, $"v.{g.Name}");
            }
            w.WriteLine("}");
            w.WriteLine("");
            return "Vars";
        }
    }

    private string GenerateClassExtension(ShaderInfo info, string vertexSrc, string fragmentSrc) {
        void writeClassDef() {
            w.WriteLine($"public partial class {info.Sym.Name} {{");
            using (w.Indent()) {
                w.WriteLine($"public static string VertexSource() => @\"{vertexSrc}\";");
                w.WriteLine($"public static string FragmentSource() => @\"{fragmentSrc}\";");
                w.WriteLine();
                w.WriteLine("public static string[] VarNames() => new string[] {");
                w.WriteLine($"    {string.Join(", ", info.Globals.Select(g => $"\"{g.Name}\""))}");
                w.WriteLine("};");
                w.WriteLine();
                var vertexType = WriteVertexInputCode(info.Vertex.Inputs);
                var varsType = WriteVarsCode(info.Globals);
                w.WriteLine($"public static ShaderConfig<{vertexType}, {varsType}> Config =>");
                w.WriteLine("    new(VertexSource(), FragmentSource(), SetShaderVars, VarNames(), VertexAttributes());");
            }
            w.WriteLine("}");
        }

        w.WriteLine("using System.Runtime.InteropServices;");
        w.WriteLine("using Silk.NET.OpenGL;");
        w.WriteLine("using DrawStuff;");
        w.WriteLine("using DrawStuff.OpenGL;");
        w.WriteLine("using static DrawStuff.ShaderLanguage;");
        w.WriteLine("");

        var shaderNamespace = info.Sym.ContainingNamespace;
        if (shaderNamespace.IsGlobalNamespace) {
            writeClassDef();
        }
        else {
            w.WriteLine($"namespace {shaderNamespace.ToDisplayString()} {{");
            writeClassDef();
            w.WriteLine("}");
        }
        return w.ToString();
    }

    public static ShaderResult GenerateClassExtension(
        List<Diagnostic> errors, TypeChecker types, ShaderInfo info, SemanticModel model)
    {
        var program = CompileIR.Compile(errors, types, model, info);
        var (vertexSrc, fragmentSrc) = errors.Any()
            ? ("[[ERROR]]", "[[ERROR]]")
            : EmitGLSL.Emit(program);
        EmitSilkGL cg = new (errors);
        var template = cg.GenerateClassExtension(info, "[[VERTEX_SRC]]", "[[FRAGMENT_SRC]]");
        cg = new (errors);
        var fullSrc = cg.GenerateClassExtension(info, vertexSrc, fragmentSrc);
        return new(info.Sym.Name, template, fullSrc, vertexSrc, fragmentSrc);
    }
}

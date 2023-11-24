
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ShaderCompiler;

static class IndentedTextWriterExt {
    public static void WithIndent(this IndentedTextWriter w, Action<IndentedTextWriter> a) {
        w.Indent += 1;
        a(w);
        w.Indent -= 1;
    }
}

public enum CompileMode {
    FragmentEntryPoint,
    VertexEntryPoint,
    SharedFunction,
}

record struct GLSLArg(string Name, ValueType Type);

class CodegenGLSL {

    private SemanticModel model;

    private List<Diagnostic> errors { get; } = new();

    private CompileMode mode;

    private ShaderInfo shader;
    private MethodInfo method;

    private GLSLArg[] Globals;
    private GLSLArg[] Inputs;
    private GLSLArg? OutputVar = null;

    Dictionary<string, string> IdentifierMap = new() {
        { "rgba", "vec4" }
    };

    public CodegenGLSL(List<Diagnostic> errors, SemanticModel model, CompileMode mode, ShaderInfo shader, MethodInfo method) {
        this.errors = errors;
        this.model = model;
        this.mode = mode;
        this.shader = shader;
        this.method = method;

        Globals = shader.Globals.Select(ToGLSLArg).ToArray();

        ArgumentInfo? fragIn = shader.Fragment.Inputs.Length > 0
            ? shader.Fragment.Inputs[0]
            : null;

        if (mode == CompileMode.VertexEntryPoint) {
            Inputs = method.Inputs.Select(ToGLSLArg).ToArray();
            var outName = fragIn is ArgumentInfo fi ? fi.Name : "_DS_frag_input";
            OutputVar = new (outName, method.Output);
        }
        else if (mode == CompileMode.FragmentEntryPoint) {
            Inputs = fragIn is ArgumentInfo fi
                ? new[] { new GLSLArg(fi.Name, fi.Type) }
                : new GLSLArg[] { }; 
            OutputVar = new GLSLArg("out_color", method.Output);
        }
        else {
            Inputs = method.Inputs.Select(ToGLSLArg).ToArray();
        }
    }

    GLSLArg ToGLSLArg(ArgumentInfo a) {
        return new(a.Name, a.Type);
    }

    SrcNode GenMemberAccess(MemberAccessExpressionSyntax m) {
        return Src.Expr(GenExpr(m.Expression), ".", m.Name.ToString());
    }

    SrcNode GenAssignment(AssignmentExpressionSyntax e) {
        return Src.Expr(GenExpr(e.Left), " = ", GenExpr(ValidateType(e.Right)));
    }

    SrcNode GenBlock(BlockSyntax block) =>
        Src.Lines(block.Statements.Select(GenStatement).ToArray());

    SrcNode GenReturn(ExpressionSyntax e) {
        if (OutputVar is GLSLArg o) {
            var assignPos = Src.Empty();
            if (mode is CompileMode.VertexEntryPoint) {
                if (method.Output is Vec4Type) {
                    assignPos = Src.Expr("gl_Position = ", o.Name, ";");
                }
                else {
                    assignPos = Src.Expr("gl_Position = ", o.Name, ".Pos;");
                }
            }
            return Src.Lines(
                Src.Expr(o.Name, " = ", GenExpr(e), ";"),
                assignPos,
                "return;"
            );
        }
        return Src.Expr("return ", GenExpr(e), ";");
    }

    SrcNode GenStatement(StatementSyntax s) {
        switch (s) {
            case BlockSyntax block: {
                return GenBlock(block);
            }
            case ExpressionStatementSyntax expr: {
                ValidateType(expr);
                return Src.Expr(GenExpr(expr.Expression), ";");
            }
            case LocalDeclarationStatementSyntax local: {
                if (local.Declaration.Variables.Count != 1)
                    throw new Exception("Declaring multiple variables in a statement is not supported");
                var v = local.Declaration.Variables[0];
                var t = GetValueType(local.Declaration.Type)!;
                return Src.Expr(
                    $"{TypeToString(t)} {v.Identifier} = ",
                    v.Initializer == null ? Src.Empty() : GenExpr(v.Initializer.Value),
                    ";"
                );
            }
            case IfStatementSyntax ifExpr: {
                return Src.Lines(
                    Src.Expr("if (", GenExpr(ifExpr.Condition), ") {"),
                    Src.Indent(GenStatement(ifExpr.Statement)),
                    "}",
                    ifExpr.Else == null ? Src.Empty() : Src.Lines(
                        "else {",
                        Src.Indent(GenStatement(ifExpr.Else.Statement)),
                        "}"
                    )
                );
            }
            case ReturnStatementSyntax ret: {
                if(ret.Expression is null) {
                    Error(ret, "Expected return value");
                    return "return;";
                }
                return GenReturn(ret.Expression);
            }
            default: {
                var msg = $"Unknown statement type '{s.GetType().Name}'";
                Error(s, msg);
                return $"[[{msg}]];";
            }
        }
    }

    ValueType? GetValueType(SyntaxNode e) {
        if(model.GetTypeInfo(e).Type is ITypeSymbol t)
            return shader.Types.TryGet(t);
        return null;
    }

    void ValidateSymbol(SyntaxNode n) {
        var sym = model.GetSymbolInfo(n).Symbol;
        if (sym is IParameterSymbol or ILocalSymbol) {
            return;
        }
        if (sym is IMethodSymbol method) {
            if(method.ContainingType.Name == "ShaderLanguage") {
                return;
            }
        }
        if (sym is IFieldSymbol field) {
            if(field.ContainingType.Name ==  this.method.Sym.ContainingType.Name) {
                return;
            }
        }
        Error(n, $"Unknown symbol {n}");
    }

    T ValidateType<T>(T n) where T : SyntaxNode {
        if (model.GetTypeInfo(n).Type is ITypeSymbol t) {
            if(shader.Types.TryGet(t) == null) {
                Error(n, $"Type '{t}' is not supported in shader code");
            }
        }
        return n;
    }

    bool IsSpecificMethod(SyntaxNode n, string methodName) {
        var sym = model.GetSymbolInfo(n).Symbol;
        return sym is IMethodSymbol method
            && method.Name == methodName
            && method.ContainingType.Name == "ShaderLanguage";
    }

    void Error(SyntaxNode n, string message) {
        errors.Add(Diagnostic.Create(ShaderDiagnostic.InvalidShader, n.GetLocation(), message));
    }

    SrcNode GenIdentifier(IdentifierNameSyntax e) {
        if (IsSpecificMethod(e, "sample")) {
            return "texture";
        }
        ValidateSymbol(e);
        var s = e.ToString();
        if (IdentifierMap.TryGetValue(s, out var glslName)) {
            return glslName;
        }
        return s;
    }

    SrcNode GenNewObj(BaseObjectCreationExpressionSyntax e) {
        var type = GetValueType(e)!;
        var args =
            e.ArgumentList?.Arguments.Select(v => GenExpr(v.Expression)).WithSeparator(", ")
            ?? new SrcNode[] { };
        if(e.Initializer != null) {
            Error(e.Initializer, "Cannot use struct initializer blocks in shaders");
        }
        return Src.Expr(TypeToString(type), "(", Src.Expr(args), ")");
    }

    SrcNode GenLiteral(LiteralExpressionSyntax e) {
        var val = model.GetConstantValue(e);
        if (val.HasValue && val.Value is float f) {
            return ((double)f).ToString();
        }
        return e.ToString();
    }

    SrcNode GenInvocation(InvocationExpressionSyntax e) {
        if(IsSpecificMethod(e, "discard") && !e.ArgumentList.Arguments.Any()) {
            if(mode != CompileMode.FragmentEntryPoint) {
                Error(e, "Can only use discard in the fragment shader");
            }
            return "discard";
        }
        var args =
            e.ArgumentList.Arguments
            .Select(a => GenExpr(a.Expression))
            .WithSeparator(", ");
        return Src.Expr(GenExpr(e.Expression), "(", Src.Expr(args), ")");
    }

    SrcNode GenExpr(ExpressionSyntax e) {
        return e switch {

            AssignmentExpressionSyntax assignExpr => GenAssignment(assignExpr),

            BinaryExpressionSyntax binExpr =>
                Src.Expr(GenExpr(binExpr.Left), " ", binExpr.OperatorToken.ToString(), " ", GenExpr(binExpr.Right)),

            PrefixUnaryExpressionSyntax prefixOp =>
                Src.Expr(prefixOp.OperatorToken.ToString(), GenExpr(prefixOp.Operand)),

            LiteralExpressionSyntax literal => GenLiteral(literal),

            MemberAccessExpressionSyntax member => GenMemberAccess(member),

            IdentifierNameSyntax identifier => GenIdentifier(identifier),

            BaseObjectCreationExpressionSyntax newObj => GenNewObj(newObj),

            ParenthesizedExpressionSyntax parenExpr => Src.Expr("(", GenExpr(parenExpr.Expression), ")"),

            InvocationExpressionSyntax invoke => GenInvocation(invoke),

            _ => $"[[unknown expr type {e.GetType().Name}, value: {e}]]",
        };
    }

    static string TypeToString(ValueType t) => t switch {
        FloatType => "float",
        Vec2Type => "vec2",
        Vec3Type => "vec3",
        Vec4Type => "vec4",
        UInt32Type => "uint",
        Mat4Type => "mat4",
        RGBAType => "vec4",
        TextureType => "sampler2D",
        CustomStruct cs => cs.Name,
        _ => throw new ShaderGenException("unknown type"),
    };

    void GenHeader(StringBuilder sb) {
        sb.AppendLine("#version 330 core");
        foreach (var u in Globals) {
            sb.AppendLine($"uniform {TypeToString(u.Type)} {u.Name};");
        }
        sb.AppendLine();
        foreach(var cs in shader.Types.CustomStructs.Values) {
            sb.AppendLine($"struct {cs.Name} {{");
            foreach(var (n, t) in cs.Fields) {
                sb.AppendLine($"    {TypeToString(t)} {n};");
            }
            sb.AppendLine("};");
            sb.AppendLine();
        }
        if (mode is CompileMode.VertexEntryPoint) {
            int vertexAttribLocation = 0;
            foreach (var i in Inputs) {
                sb.AppendLine($"layout (location = {vertexAttribLocation++}) in {TypeToString(i.Type)} {i.Name};");
            }
        }
        else {
            foreach (var i in Inputs) {
                if(i.Type is UInt32Type && mode is CompileMode.FragmentEntryPoint)
                    sb.Append($"flat ");
                sb.AppendLine($"in {TypeToString(i.Type)} {i.Name};");
            }
        }
        if(OutputVar is GLSLArg o) {
            if (o.Type is UInt32Type && mode is CompileMode.VertexEntryPoint)
                sb.Append($"flat ");
            sb.AppendLine($"out {TypeToString(o.Type)} {o.Name};");
        }
    }

    MethodDeclarationSyntax GetDeclarationSyntax(IMethodSymbol sym) =>
        (MethodDeclarationSyntax)sym.DeclaringSyntaxReferences.First().GetSyntax();

    string GenShader() {
        // Create an IndentedTextWriter and set the tab string to use
        // as the indentation string for each indentation level.
        var sb = new StringBuilder();
        GenHeader(sb);

        SrcNode body;
        var decl = GetDeclarationSyntax(method.Sym);
        if (decl.Body is not null) {
            body = GenBlock(decl.Body);
        }
        else if (decl.ExpressionBody is not null) {
            body = GenReturn(decl.ExpressionBody.Expression);
        }
        else {
            Error(decl, "Method has no body");
            body = "[[empty]]";
        }
        var main = Src.Lines(
            "void main() {",
            Src.Indent(body),
            "}"
        );
        main.WriteTree(sb);
        return sb.ToString();
    }

    public static string Compile(
        List<Diagnostic> errors, SemanticModel model, CompileMode mode, ShaderInfo shader, MethodInfo method)
    {
        var compiler = new CodegenGLSL(errors, model, mode, shader, method);
        return compiler.GenShader();
    }
}

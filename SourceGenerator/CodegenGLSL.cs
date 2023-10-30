
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

record struct GLSLArg(ArgKind Kind, string Name, string GLSLName, ValType Type);

class CodegenGLSL {

    private SemanticModel model;

    private List<Diagnostic> errors { get; } = new();

    private CompileMode mode;

    private MethodInfo method;

    private GLSLArg[] Globals;
    private GLSLArg[] Inputs;
    private GLSLArg[] Outputs;

    Dictionary<string, GLSLArg> AllArgs = new();

    public CodegenGLSL(List<Diagnostic> errors, SemanticModel model, CompileMode mode, MethodInfo method, ArgumentInfo[] globals) {
        this.errors = errors;
        this.model = model;
        this.mode = mode;
        this.method = method;

        Globals = globals.Select(ToGLSLArg).ToArray();
        Inputs = method.Inputs.Select(ToGLSLArg).ToArray();
        Outputs = method.Outputs.Select(ToGLSLArg).ToArray();

        foreach (var a in Globals) AllArgs.Add(a.Name, a);
        foreach (var a in Inputs) AllArgs.Add(a.Name, a);
        foreach (var a in Outputs) AllArgs.Add(a.Name, a);
    }

    GLSLArg ToGLSLArg(ArgumentInfo a) {
        if (mode is CompileMode.FragmentEntryPoint && a.Type is ValType.RGBA && a.Kind == ArgKind.Output) {
            return new(a.Kind, a.Name, "out_color", a.Type);
        }
        if(mode is CompileMode.VertexEntryPoint && a.Type is ValType.VertexPos && a.Kind == ArgKind.Output) {
            return new(a.Kind, a.Name, "gl_Position", a.Type);
        }
        return new(a.Kind, a.Name, a.Name, a.Type);
    }

    string GenMemberAccess(MemberAccessExpressionSyntax m) {
        return $"{GenExpr(m.Expression)}.{m.Name}";
    }

    string GenAssignment(AssignmentExpressionSyntax e) {
        return $"{GenExpr(e.Left)} = {GenExpr(ValidateType(e.Right))}";
    }

    void GenBlock(IndentedTextWriter w, BlockSyntax block) {
        foreach (var s in block.Statements)
            GenStatement(w, s);
    }

    void GenStatement(IndentedTextWriter w, StatementSyntax s) {
        switch(s) {
            case ExpressionStatementSyntax expr: {
                ValidateType(expr);
                w.Write(GenExpr(expr.Expression));
                w.WriteLine(";");
                break;
            }
            case LocalDeclarationStatementSyntax local: {
                if (local.Declaration.Variables.Count != 1)
                    throw new Exception("Declaring multiple variables in a statement is not supported");
                var v = local.Declaration.Variables[0];
                var t = GetValType(local.Declaration.Type)!.Value;
                w.Write($"{TypeToString(t)} {v.Identifier} = ");
                if(v.Initializer != null) {
                    w.Write(GenExpr(v.Initializer.Value));
                }
                w.WriteLine(";");
                break;
            }
            case IfStatementSyntax ifExpr: {
                w.WriteLine($"if ({GenExpr(ifExpr.Condition)}) {{");
                w.WithIndent(w => GenStatement(w, ifExpr.Statement));
                w.WriteLine("}");
                if(ifExpr.Else != null) {
                    w.WriteLine("else {");
                    w.WithIndent(w => GenStatement(w, ifExpr.Else.Statement));
                    w.WriteLine("}");
                }
                break;
            }
            default: {
                w.WriteLine($"[[Error: unknown statement type: '{s.GetType()}']];");
                break;
            }
        }
    }

    ValType? GetValType(SyntaxNode e) {
        if(model.GetTypeInfo(e).Type is ITypeSymbol t)
            return ShaderAnalyze.ToShaderType(t);
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
            if(ShaderAnalyze.ToShaderType(t) == null) {
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
        errors.Add(Diagnostic.Create(Diagnostics.InvalidShader, n.GetLocation(), message));
    }

    string GenIdentifier(IdentifierNameSyntax e) {
        if(IsSpecificMethod(e, "sample")) {
            return "texture";
        }
        ValidateSymbol(e);
        var s = e.ToString();
        if(AllArgs.TryGetValue(s, out var a)) {
            return a.GLSLName;
        }
        return s;
    }

    string GenNewObj(BaseObjectCreationExpressionSyntax e) {
        var type = GetValType(e)!.Value;
        string args = string.Join(", ", e.ArgumentList?.Arguments.Select(v => GenExpr(v.Expression)));
        return $"{TypeToString(type)}({args})";
    }

    string GenLiteral(LiteralExpressionSyntax e) {
        var val = model.GetConstantValue(e);
        if (val.HasValue && val.Value is float f) {
            return ((double)f).ToString();
        }
        return e.ToString();
    }

    string GenInvocation(InvocationExpressionSyntax e) {
        if(IsSpecificMethod(e, "discard") && !e.ArgumentList.Arguments.Any()) {
            if(mode != CompileMode.FragmentEntryPoint) {
                Error(e, "Can only use discard in the fragment shader");
            }
            return "discard";
        }
        var args = e.ArgumentList.Arguments.Select(a => GenExpr(a.Expression));
        return $"{GenExpr(e.Expression)}({string.Join(", ", args)})";
    }

    string GenExpr(ExpressionSyntax e) {
        return e switch {

            AssignmentExpressionSyntax assignExpr => GenAssignment(assignExpr),

            BinaryExpressionSyntax binExpr =>
                $"{GenExpr(binExpr.Left)} {binExpr.OperatorToken} {GenExpr(binExpr.Right)}",

            PrefixUnaryExpressionSyntax prefixOp =>
                $"{prefixOp.OperatorToken} {GenExpr(prefixOp.Operand)}",

            LiteralExpressionSyntax literal => GenLiteral(literal),

            MemberAccessExpressionSyntax member => GenMemberAccess(member),

            IdentifierNameSyntax identifier => GenIdentifier(identifier),

            BaseObjectCreationExpressionSyntax newObj => GenNewObj(newObj),

            ParenthesizedExpressionSyntax parenExpr => $"({GenExpr(parenExpr.Expression)})",

            InvocationExpressionSyntax invoke => GenInvocation(invoke),

            _ => $"[[unknown expr type {e.GetType().Name}, value: {e}]]",
        };
    }

    static string TypeToString(ValType t) => t switch {
        ValType.RGBA => "vec4",
        ValType.VertexPos => "vec4",
        ValType.TextureHandle => "sampler2D",
        ValType.UInt32 => "uint",
        _ => t.ToString().ToLower(),
    };

    void GenHeader(IndentedTextWriter w) {
        w.WriteLine("#version 330 core");
        foreach (var u in Globals) {
            w.WriteLine($"uniform {TypeToString(u.Type)} {u.GLSLName};");
        }
        if (mode is CompileMode.VertexEntryPoint) {
            int vertexAttribLocation = 0;
            foreach (var i in Inputs) {
                w.WriteLine($"layout (location = {vertexAttribLocation++}) in {TypeToString(i.Type)} {i.GLSLName};");
            }
        }
        else {
            foreach (var i in Inputs) {
                if(i.Type == ValType.UInt32 && mode is CompileMode.FragmentEntryPoint)
                    w.Write($"flat ");
                w.WriteLine($"in {TypeToString(i.Type)} {i.GLSLName};");
            }
        }
        foreach (var o in Outputs) {
            if (o.Type == ValType.UInt32 && mode is CompileMode.VertexEntryPoint)
                w.Write($"flat ");
            w.WriteLine($"out {TypeToString(o.Type)} {o.GLSLName};");
        }
    }

    MethodDeclarationSyntax GetDeclarationSyntax(IMethodSymbol sym) =>
        (MethodDeclarationSyntax)sym.DeclaringSyntaxReferences.First().GetSyntax();

    string GenShader() {
        var buffer = new StringWriter();

        // Create an IndentedTextWriter and set the tab string to use
        // as the indentation string for each indentation level.
        var w = new IndentedTextWriter(buffer, "    ");
        GenHeader(w);
        w.WriteLine("void main() {");
        w.WithIndent(w => {
            var decl = GetDeclarationSyntax(method.Sym);
            if (decl.Body is not null) {
                GenBlock(w, decl.Body);
            }
            else if (decl.ExpressionBody is not null) {
                w.WriteLine($"{GenExpr(decl.ExpressionBody!.Expression)};");
            }
            else {
                w.WriteLine("[[error: method has no body]]");
            }
        });
        w.WriteLine("}");
        return buffer.ToString();
    }

    public static string Compile(List<Diagnostic> errors, SemanticModel model, CompileMode mode, MethodInfo method, ArgumentInfo[] globals) {
        var compiler = new CodegenGLSL(errors, model, mode, method, globals);
        return compiler.GenShader();
    }
}

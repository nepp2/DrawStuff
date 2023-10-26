
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

    private CompileMode mode;

    private MethodInfo method;

    private GLSLArg[] Globals;
    private GLSLArg[] Inputs;
    private GLSLArg[] Outputs;

    Dictionary<string, GLSLArg> AllArgs = new();

    public CodegenGLSL(SemanticModel model, CompileMode mode, MethodInfo method, ArgumentInfo[] globals) {
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
        return $"{GenExpr(e.Left)} = {GenExpr(e.Right)}";
    }

    void GenBlock(IndentedTextWriter w, BlockSyntax block) {
        foreach (var s in block.Statements)
            GenStatement(w, s);
    }

    void GenStatement(IndentedTextWriter w, StatementSyntax s) {
        switch(s) {
            case ExpressionStatementSyntax expr: {
                w.Write(GenExpr(expr.Expression));
                w.WriteLine(";");
                break;
            }
            default: {
                w.WriteLine("[[Error: unknown statement type]];");
                break;
            }
        }
    }

    ValType? GetValType(ExpressionSyntax e) {
        if(model.GetTypeInfo(e).Type is ITypeSymbol t)
            return ShaderAnalyze.ToShaderType(t);
        return null;
    }

    string GenIdentifier(IdentifierNameSyntax e) {
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

    string GenExpr(ExpressionSyntax e) {
        return e switch {

            AssignmentExpressionSyntax assignExpr => GenAssignment(assignExpr),

            BinaryExpressionSyntax binExpr =>
                $"{GenExpr(binExpr.Left)} {binExpr.OperatorToken} {GenExpr(binExpr.Right)}",

            LiteralExpressionSyntax literal => GenLiteral(literal),

            MemberAccessExpressionSyntax member => GenMemberAccess(member),

            IdentifierNameSyntax identifier => GenIdentifier(identifier),

            BaseObjectCreationExpressionSyntax newObj => GenNewObj(newObj),

            _ => $"[[unknown expr type {e.GetType().Name}, value: {e}]]",
        };
    }

    static string TypeToString(ValType t) => t switch {
        ValType.RGBA => "vec4",
        ValType.VertexPos => "vec4",
        _ => t.ToString().ToLower(),
    };

    void GenHeader(IndentedTextWriter w) {
        w.WriteLine("#version 330 core");
        int loc = 0;
        foreach (var u in Globals) {
            w.WriteLine($"uniform {TypeToString(u.Type)} {u.GLSLName};");
        }
        foreach (var i in Inputs) {
            w.WriteLine($"layout (location = {loc++}) in {TypeToString(i.Type)} {i.GLSLName};");
        }
        foreach (var o in Outputs) {
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

    public static string Compile(SemanticModel model, CompileMode mode, MethodInfo method, ArgumentInfo[] globals) {
        var compiler = new CodegenGLSL(model, mode, method, globals);
        return compiler.GenShader();
    }
}

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ShaderCompiler;

public enum CompileMode {
    Vertex,
    Fragment,
    Library,
}

public class CompileIR {

    TypeChecker Types;
    SemanticModel Model;
    List<Diagnostic> Errors { get; } = new();
    CompileMode Mode;
    ShaderInfo Shader;
    MethodInfo Method;

    Dictionary<string, MethodInfo> Helpers;
    HashSet<string> HelpersUsed = new();
    Queue<MethodInfo> HelperQueue = new();

    ImmutableArray<NamedValue> Inputs;

    public CompileIR(
        List<Diagnostic> errors,
        TypeChecker types,
        SemanticModel model,
        CompileMode mode,
        ShaderInfo shader,
        MethodInfo method) {
        Errors = errors;
        Types = types;
        Model = model;
        Mode = mode;
        Shader = shader;
        Method = method;
        Helpers = shader.Helpers.ToDictionary(h => h.Sym.Name, h => h);
    }

    IR.Statement.Block GenBlock(BlockSyntax block) =>
        new(block.Statements.Select(GenStatement).ToImmutableArray());

    IR.Statement GenReturn(ExpressionSyntax e) =>
        new IR.Statement.Return(GenExpr(e));

    IR.Statement GenStatement(StatementSyntax s) {
        switch (s) {
            case BlockSyntax block: {
                return GenBlock(block);
            }
            case ExpressionStatementSyntax expr: {
                ValidateType(expr);
                return new IR.Statement.Expression(GenExpr(expr.Expression));
            }
            case LocalDeclarationStatementSyntax local: {
                if (local.Declaration.Variables.Count != 1)
                    throw new Exception("Declaring multiple variables in a statement is not supported");
                var v = local.Declaration.Variables[0];
                var t = GetValueType(local.Declaration.Type)!;
                var expr = v.Initializer == null ? null : GenExpr(v.Initializer.Value);
                return new IR.Statement.DeclareLocal(t, v.Identifier.ToString(), expr);
            }
            case IfStatementSyntax ifExpr: {
                var cond = GenExpr(ifExpr.Condition);
                var thenDo = GenStatement(ifExpr.Statement);
                var elseDo = ifExpr.Else == null ? null : GenStatement(ifExpr.Else.Statement);
                return new IR.Statement.If(cond, thenDo, elseDo);
            }
            case ReturnStatementSyntax ret: {
                if (ret.Expression is null) {
                    return new IR.Statement.Return(null);
                }
                return GenReturn(ret.Expression);
            }
            default: {
                return ErrorStatement(s, $"Unknown statement type '{s.GetType().Name}'");
            }
        }
    }

    TypeTag? GetValueType(SyntaxNode e) {
        if (Model.GetTypeInfo(e).Type is ITypeSymbol sym && Types.TryGet(sym, out var t))
            return t;
        return null;
    }

    void ValidateSymbol(SyntaxNode n) {
        var sym = Model.GetSymbolInfo(n).Symbol;
        if (sym is IParameterSymbol or ILocalSymbol)
            return;
        if (sym is IMethodSymbol method) {
            if (method.ContainingType.Name == "ShaderLanguage")
                return;
            if (method.ContainingType.Name == this.Method.Sym.ContainingType.Name) {
                if (Helpers.TryGetValue(method.Name, out var h)) {
                    if (HelpersUsed.Add(method.Name)) {
                        HelperQueue.Enqueue(h);
                    }
                    return;
                }
            }
        }
        if (sym is IFieldSymbol field) {
            if (field.ContainingType.Name == this.Method.Sym.ContainingType.Name)
                return;
        }
        Error(n, $"Unknown symbol {n}");
    }

    T ValidateType<T>(T n) where T : SyntaxNode {
        if (Model.GetTypeInfo(n).Type is ITypeSymbol t) {
            if (!Types.TryGet(t, out var _)) {
                Error(n, $"Type '{t}' is not supported in shader code");
            }
        }
        return n;
    }

    IMethodSymbol? TryGetBuiltinMethod(SyntaxNode n) {
        var sym = Model.GetSymbolInfo(n).Symbol;
        if(sym is IMethodSymbol method && method.ContainingType.Name == "ShaderLanguage")
            return method;
        return null;
    }

    bool IsSpecificBuiltin(SyntaxNode n, string name) =>
        TryGetBuiltinMethod(n) is IMethodSymbol m && m.Name == name;

    void Error(SyntaxNode n, string message) {
        Errors.Add(Diagnostic.Create(ShaderDiagnostic.InvalidShader, n.GetLocation(), message));
    }

    T ErrorValue<T>(SyntaxNode n, string message, T errVal) {
        Error(n, message);
        return errVal;
    }

    IR.Expr.Error ErrorExpr(SyntaxNode n, string message) =>
        ErrorValue<IR.Expr.Error>(n, message, new());

    IR.Statement.Error ErrorStatement(SyntaxNode n, string message) =>
        ErrorValue<IR.Statement.Error>(n, message, new());

    IR.Expr GenIdentifier(IdentifierNameSyntax e) {
        if (TryGetBuiltinMethod(e) is IMethodSymbol method) {
            if(method.Name == "sample")
                return new IR.Expr.Intrinsic(IR.IntrinsicOp.TextureSample);
            if (method.Name == "rgba")
                return new IR.Expr.Intrinsic(IR.IntrinsicOp.RGBAConstruct);
        }
        ValidateSymbol(e);
        return new IR.Expr.Identifier(e.ToString());
    }

    IR.Expr.FieldAccess GenFieldAccess(MemberAccessExpressionSyntax m) =>
        new(GenExpr(m.Expression), m.Name.ToString());

    IR.Expr.Assignment GenAssignment(AssignmentExpressionSyntax e) =>
        new(GenExpr(e.Left), GenExpr(ValidateType(e.Right)));

    IR.Expr.Construct GenNewObj(BaseObjectCreationExpressionSyntax e) {
        var type = GetValueType(e)!;
        var args =
            e.ArgumentList?.Arguments.Select(v => GenExpr(v.Expression)).ToImmutableArray()
            ?? ImmutableArray<IR.Expr>.Empty;
        if (e.Initializer != null) {
            Error(e.Initializer, "Cannot use struct initializer blocks in shaders");
        }
        return new(type, args);
    }

    IR.Expr GenLiteral(LiteralExpressionSyntax e) {
        var val = Model.GetConstantValue(e);
        if (!val.HasValue)
            return ErrorExpr(e, "Invalid literal");
        return val.Value! switch {
            float f => new IR.Expr.LiteralFloat(f),
            bool b => new IR.Expr.LiteralBool(b),
            int i => new IR.Expr.LiteralI32(i),
            uint i => new IR.Expr.LiteralU32(i),
            double i => ErrorExpr(e, $"Double precision floats are not supported in shader code"),
            var v => ErrorExpr(e, $"Unexpected literal type '{v.GetType()}'"),
        };
    }

    IR.Expr GenInvocation(InvocationExpressionSyntax e) {
        if (IsSpecificBuiltin(e.Expression, "discard") && !e.ArgumentList.Arguments.Any()) {
            if (Mode != CompileMode.Fragment) {
                Error(e, "Can only use discard in the fragment shader");
            }
            return new IR.Expr.Intrinsic(IR.IntrinsicOp.Discard);
        }
        var args =
            e.ArgumentList.Arguments
            .Select(a => GenExpr(a.Expression))
            .ToImmutableArray();
        return new IR.Expr.Invoke(GenExpr(e.Expression), args);
    }

    IR.Op ToOperator(SyntaxNode n, string op) => op switch {
        "+" => IR.Op.Plus,
        "-" => IR.Op.Minus,
        "*" => IR.Op.Multiply,
        "/" => IR.Op.Divide,
        "<<" => IR.Op.ShiftLeft,
        ">>" => IR.Op.ShiftRight,
        "&" => IR.Op.BitAnd,
        "|" => IR.Op.BitOr,
        "==" => IR.Op.Equals,
        "!" => IR.Op.Not,
        "!=" => IR.Op.NotEquals,
        "<" => IR.Op.LessThan,
        ">" => IR.Op.GreaterThan,
        ">=" => IR.Op.GreaterThanOrEqual,
        "<=" => IR.Op.LessThanOrEqual,
        _ => ErrorValue<IR.Op>(n, $"operator '{op}' not supported", default),
    };

    IR.Expr.BinOp GenBinOp(BinaryExpressionSyntax e) =>
        new(GenExpr(e.Left), ToOperator(e, e.OperatorToken.Text), GenExpr(e.Right));

    IR.Expr.PrefixOp GenPrefixOp(PrefixUnaryExpressionSyntax e) =>
        new(ToOperator(e, e.OperatorToken.Text), GenExpr(e.Operand));

    IR.Expr GenExpr(ExpressionSyntax e) {
        return e switch {
            AssignmentExpressionSyntax assignExpr => GenAssignment(assignExpr),
            BinaryExpressionSyntax binExpr => GenBinOp(binExpr),
            PrefixUnaryExpressionSyntax prefixOp => GenPrefixOp(prefixOp),
            LiteralExpressionSyntax literal => GenLiteral(literal),
            MemberAccessExpressionSyntax member => GenFieldAccess(member),
            IdentifierNameSyntax identifier => GenIdentifier(identifier),
            BaseObjectCreationExpressionSyntax newObj => GenNewObj(newObj),
            ParenthesizedExpressionSyntax parenExpr =>
                new IR.Expr.Paren(GenExpr(parenExpr.Expression)),
            InvocationExpressionSyntax invoke => GenInvocation(invoke),
            _ => ErrorExpr(e, $"unknown expr type {e.GetType().Name}, value: {e}")
        };
    }

    MethodDeclarationSyntax GetDeclarationSyntax(IMethodSymbol sym) =>
        (MethodDeclarationSyntax)sym.DeclaringSyntaxReferences.First().GetSyntax();

    IR.Function GenerateFunction() {
        IR.Statement.Block body;
        var decl = GetDeclarationSyntax(Method.Sym);
        if (decl.Body is not null) {
            body = GenBlock(decl.Body);
        }
        else if (decl.ExpressionBody is not null) {
            var ret = GenReturn(decl.ExpressionBody.Expression);
            body = new(ImmutableArray.Create(ret));
        }
        else {
            IR.Statement error = ErrorStatement(decl, "function has no body");
            body = new(ImmutableArray.Create(error)); ;
        }
        var args =
            Method.Inputs
            .Select(i => new NamedValue(i.Name, i.Type))
            .ToImmutableArray();
        return new(Method.Sym.Name, Method.Output, args, body);
    }

    ImmutableArray<IR.Function> GenerateHelpers() {
        List<IR.Function> funcs = new();
        while (HelperQueue.Count > 0) {
            var m = HelperQueue.Dequeue();
            var cg = new CompileIR(Errors, Types, Model, CompileMode.Library, Shader, m);
            funcs.Add(cg.GenerateFunction());
        }
        funcs.Reverse();
        return funcs.ToImmutableArray();
    }

    static IR.Shader CompileShader(
        List<Diagnostic> errors, TypeChecker types, SemanticModel model, CompileMode mode, ShaderInfo shader, MethodInfo method) {
        var compiler = new CompileIR(errors, types, model, mode, shader, method);
        var globals =
            shader.Globals
            .Select(a => new NamedValue(a.Name, a.Type))
            .ToImmutableArray();
        var main = compiler.GenerateFunction();
        var helpers = compiler.GenerateHelpers();
        return new(globals, shader.CustomStructs, helpers, main);
    }

    public static IR.Program Compile(
        List<Diagnostic> errors, TypeChecker types, SemanticModel model, ShaderInfo shader) {
        return new(
            CompileShader(errors, types, model, CompileMode.Vertex, shader, shader.Vertex),
            CompileShader(errors, types, model, CompileMode.Fragment, shader, shader.Fragment));
    }
}

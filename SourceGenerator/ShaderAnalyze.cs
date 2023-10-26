using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ShaderCompiler;

public enum ValType {
    Float,
    Vec2,
    Vec3,
    Vec4,
    UInt32,
    Mat4,
    RGBA,
    VertexPos,
}

public enum ArgKind {
    Input,
    Output,
    Uniform,
}

public struct ClassInfo {
    public ITypeSymbol Type;
    public ClassDeclarationSyntax Syntax;
}

public record struct ArgumentInfo(ArgKind Kind, string Name, ValType Type);

public class MethodInfo {
    public IMethodSymbol Sym;
    public List<ArgumentInfo> Configs;
    public List<ArgumentInfo> Inputs;
    public List<ArgumentInfo> Outputs;
}

public class Diagnostics {
    public static DiagnosticDescriptor InvalidShader =
        new DiagnosticDescriptor(id: "SHDRGN001",
            title: "Invalid shader",
            messageFormat: "{0}",
            category: "ShaderGen",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
}

public class ShaderAnalyze {

    private GeneratorExecutionContext Ctx { get; }
    public ITypeSymbol Sym { get; }
    public ClassDeclarationSyntax Syntax { get; }
    private Dictionary<string, ISymbol> Members;
    private List<Diagnostic> Errors;

    public MethodInfo Vertex;
    public MethodInfo Fragment;
    public ArgumentInfo[] Configs;

    public ShaderAnalyze(List<Diagnostic> errors, ClassInfo c) {
        Sym = c.Type;
        Syntax = c.Syntax;
        Members = c.Type.GetMembers().ToDictionary(m => m.Name);
        Errors = errors;
        Process();
    }

    private void Error(string message, Location? loc = null) {
        Errors.Add(Diagnostic.Create(Diagnostics.InvalidShader, loc ?? Sym.Locations[0], message));
    }

    class Methods {
        public IMethodSymbol Vert;
        public IMethodSymbol Frag;
    }

    bool Success<T>(out T result, in T val) {
        result = val;
        return true;
    }

    bool Fail<T>(out T result) {
        result = default!;
        Debug.Assert(Errors.Count > 0);
        return false;
    }

    bool Fail<T>(out T result, string error, Location? loc = null) {
        Error(error, loc);
        return Fail(out result);
    }

    public static ValType? ToShaderType(ITypeSymbol sym) {
        if (sym is INamedTypeSymbol type) {
            switch (type.ToDisplayString()) {
                case "System.Single" or "float": return ValType.Float;
                case "System.UInt32": return ValType.UInt32;
                case "DrawStuff.ShaderLang.Vec2": return ValType.Vec2;
                case "DrawStuff.ShaderLang.Vec3": return ValType.Vec3;
                case "DrawStuff.ShaderLang.Vec4": return ValType.Vec4;
                case "DrawStuff.ShaderLang.Mat4": return ValType.Mat4;
                case "DrawStuff.ShaderLang.RGBA": return ValType.RGBA;
                case "DrawStuff.ShaderLang.VertexPos": return ValType.VertexPos;
                default: break;
            };
        }
        return null;
    }

    private bool ValidateType(ITypeSymbol sym, Location loc, out ValType r) {
        if(ToShaderType(sym) is ValType t)
            return Success(out r, t);
        return Fail(out r, $"Type {sym.ToDisplayString()} is not supported in shaders", loc);
    }

    static bool IsStaticVar(ITypeSymbol sym, out ITypeSymbol inner) {
        if (sym is INamedTypeSymbol type && type.Name == "StaticVar") {
            inner = type.TypeArguments[0];
            return true;
        }
        inner = default!;
        return false;
    }

    private bool GetMethod(string name, out MethodInfo method) {
        if (!(Members.TryGetValue(name, out var v) && v is IMethodSymbol m && m.IsStatic)) {
            return Fail(out method, $"shader must have a static `{name}` method");
        }
        var syntax = m.DeclaringSyntaxReferences[0].GetSyntax() as MethodDeclarationSyntax;
        var configs = new List<ArgumentInfo>();
        var inputs = new List<ArgumentInfo>();
        var outputs = new List<ArgumentInfo>();
        foreach(var p in m.Parameters) {
            switch(p.RefKind) {
                case RefKind.In: {
                    if (IsStaticVar(p.Type, out var innerType)) {
                        if (ValidateType(innerType, p.Locations[0], out var r)) {
                            configs.Add(new(ArgKind.Input, p.Name, r));
                        }
                    }
                    else if (ValidateType(p.Type, p.Locations[0], out var r)) {
                        inputs.Add(new(ArgKind.Uniform, p.Name, r));
                    }
                    break;
                }
                case RefKind.Out: {
                    if (ValidateType(p.Type, p.Locations[0], out var r)) {
                        outputs.Add(new(ArgKind.Output, p.Name, r));
                    }
                    break;
                }
                default:
                    return Fail(out method, $"Unsupported parameter ref kind {p.RefKind}", p.Locations[0]);
            }
        }
        if(!m.ReturnsVoid) {
            return Fail(out method,
                "Method must return void",
                syntax?.ReturnType.GetLocation() ?? m.Locations[0]);
        }
        var info = new MethodInfo() { Sym = m, Configs = configs, Inputs = inputs, Outputs = outputs };
        return Success(out method, info);
    }

    private bool GetVertexMethod(out MethodInfo method) {
        if (!GetMethod("Vertex", out var vert)) return Fail(out method);
        return Success(out method, vert);
    }

    private bool GetFragmentMethod(out MethodInfo method) {
        if (!GetMethod("Fragment", out var frag)) return Fail(out method);
        return Success(out method, frag);
    }

    private void Process() {
        if (Sym.DeclaredAccessibility != Accessibility.Public || !Sym.IsStatic) {
            Error("shader must be a public static class");
            return;
        }
        if (!Syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))) {
            Error("shader class must be declared with the `partial` keyword");
            return;
        }
        if (!GetVertexMethod(out Vertex))
            return;
        if (!GetFragmentMethod(out Fragment))
            return;

        var configs = new Dictionary<string, ArgumentInfo>();
        var configIter = Enumerable.Empty<ArgumentInfo>();
        if (Vertex != null) configIter = configIter.Concat(Vertex.Configs);
        if (Fragment != null) configIter = configIter.Concat(Fragment.Configs);
        foreach (var u in configIter) {
            if (configs.TryGetValue(u.Name, out var prev)) {
                if (u.Type != prev.Type) {
                    Error($"Config `{u.Name}` referenced with two or more different types");
                    return;
                }
            }
            else {
                configs[u.Name] = u;
            }
        }
        Configs = configs.Values.OrderBy(u => u.Name).ToArray();
    }

    public static bool Process(List<Diagnostic> errors, ClassInfo c, out ShaderAnalyze result) {
        var info = new ShaderAnalyze(errors, c);
        if (info.Errors.Count > 0) {
            result = null;
            return false;
        }
        else {
            result = info;
            return true;
        }
    }

}

public class ShaderGenException : Exception {
    public ShaderGenException(string msg) : base(msg) { }
}

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections.Immutable;
using System.Reflection;

namespace ShaderCompiler;

public interface ValueType {}
public interface BuiltinType : ValueType {}

public record FloatType() : BuiltinType;
public record Vec2Type() : BuiltinType;
public record Vec3Type() : BuiltinType;
public record Vec4Type() : BuiltinType;
public record UInt32Type() : BuiltinType;
public record Mat4Type() : BuiltinType;
public record RGBAType() : BuiltinType;
public record TextureType() : BuiltinType;

public static class BuiltinTypes {
    public static FloatType Float = new FloatType();
    public static Vec2Type Vec2 = new Vec2Type();
    public static Vec3Type Vec3 = new Vec3Type();
    public static Vec4Type Vec4 = new Vec4Type();
    public static UInt32Type UInt32 = new UInt32Type();
    public static Mat4Type Mat4 = new Mat4Type();
    public static RGBAType RGBA = new RGBAType();
    public static TextureType Texture2D = new TextureType();
}

public record CustomStruct(string Name, ImmutableArray<(string, ValueType)> Fields) : ValueType;

public record struct ClassInfo(ITypeSymbol Type, ClassDeclarationSyntax Syntax, SemanticModel Model);

public record struct ArgumentInfo(string Name, ValueType Type, Location Loc);

public record MethodInfo(IMethodSymbol Sym, ArgumentInfo[] Inputs, ValueType Output);

// TODO: deal with this warning properly
[System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisReleaseTracking", "RS2008")]
public class ShaderDiagnostic {

    public static DiagnosticDescriptor InvalidShader =
        new DiagnosticDescriptor(id: "SHDRGN001",
            title: "Invalid shader",
            messageFormat: "{0}",
            category: "ShaderGen",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    public static DiagnosticDescriptor DebugDiagnostic =
        new DiagnosticDescriptor(id: "SHDRGN002",
            title: "Debug info",
            messageFormat: "{0}",
            category: "ShaderGen",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

}

public record ShaderInfo(
    ITypeSymbol Sym, ClassDeclarationSyntax Syntax,
    MethodInfo Vertex, MethodInfo Fragment, ArgumentInfo[] Globals,
    ShaderTypes Types);

public class ShaderTypes {

    public Dictionary<string, CustomStruct> CustomStructs = new();

    public ValueType? ToBuiltinType(ITypeSymbol sym) => sym.ToDisplayString() switch {
        "System.Single" or "float" => BuiltinTypes.Float,
        "System.UInt32" or "uint" => BuiltinTypes.UInt32,
        "DrawStuff.ShaderLanguage.Vec2" => BuiltinTypes.Vec2,
        "DrawStuff.ShaderLanguage.Vec3" => BuiltinTypes.Vec3,
        "DrawStuff.ShaderLanguage.Vec4" => BuiltinTypes.Vec4,
        "DrawStuff.ShaderLanguage.Mat4" => BuiltinTypes.Mat4,
        "DrawStuff.ShaderLanguage.RGBA" => BuiltinTypes.RGBA,
        "DrawStuff.ShaderLanguage.Texture2D" => BuiltinTypes.Texture2D,
        _ => null,
    };

    public ValueType? TryGet(ITypeSymbol sym) {
        ValueType? t = ToBuiltinType(sym);
        if (t is null) {
            if (CustomStructs.TryGetValue(sym.ToDisplayString(), out var cs)) {
                t = cs;
            }
        }
        return t;
    }
}

public class ShaderAnalyze {

    public ITypeSymbol Sym { get; }
    public ClassDeclarationSyntax Syntax { get; }
    private List<ISymbol> Members = new();
    private List<Diagnostic> Errors;
    private ShaderTypes Types = new();

    public ShaderAnalyze(List<Diagnostic> diagnostics, ClassInfo c) {
        Sym = c.Type;
        Syntax = c.Syntax;
        Errors = diagnostics;
        foreach(var m in c.Type.GetMembers()) {
            bool include = m.DeclaringSyntaxReferences.Any(s => s.SyntaxTree == c.Syntax.SyntaxTree);
            if (include) {
                Members.Add(m);
            }
        }
    }

    private SyntaxNode? GetLocalDeclaration(ISymbol sym) {
        foreach (var n in sym.DeclaringSyntaxReferences) {
            if (n.SyntaxTree == Syntax.SyntaxTree)
                return n.GetSyntax();
        }
        return null;
    }

    private Location SymbolLoc(ISymbol sym) {
        if (GetLocalDeclaration(sym) is SyntaxNode n)
            return n.GetLocation();
        return Syntax.GetLocation();
    }

    private void Error(string message, Location? loc = null) {
        Errors.Add(Diagnostic.Create(ShaderDiagnostic.InvalidShader, loc ?? Syntax.GetLocation(), message));
    }

    private void Error(string message, ISymbol sym) {
        Errors.Add(Diagnostic.Create(ShaderDiagnostic.InvalidShader, SymbolLoc(sym), message));
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

    bool Fail<T>(out T result, string error, ISymbol sym) {
        Error(error, sym);
        return Fail(out result);
    }

    private bool ValidateType(ITypeSymbol sym, ISymbol loc, out ValueType r) {
        if(Types.TryGet(sym) is ValueType t)
            return Success(out r, t);
        return Fail(out r, $"Type {sym.ToDisplayString()} is not supported in shaders", loc);
    }

    private bool GetMethod(IMethodSymbol m, out MethodInfo method) {
        var inputs = new List<ArgumentInfo>();
        foreach(var p in m.Parameters) {
            if (ValidateType(p.Type, p, out var r)) {
                inputs.Add(new(p.Name, r, SymbolLoc(p)));
            }
            if(p.RefKind is not (RefKind.None or RefKind.In)) {
                Error($"Unsupported parameter ref kind {p.RefKind}", p);
            }
        }
        ValidateType(m.ReturnType, m, out var returnType);
        var info = new MethodInfo(m, inputs.ToArray(), returnType);
        return Success(out method, info);
    }

    private void GetUniqueMethod(IMethodSymbol m, ref MethodInfo? vertex) {
        if(vertex != null) {
            Error($"Function '{m.Name}' can only be defined once, but more than one definition was found", m);
            return;
        }
        GetMethod(m, out vertex);
    }

    private void GetHelperMethod(IMethodSymbol m, List<MethodInfo> helpers) {
        if(GetMethod(m, out var info)) {
            helpers.Add(info);
        }
    }

    public bool Process(out ShaderInfo result) {
        if (!Syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))) {
            return Fail(out result, "shader class must be declared with the `partial` keyword");
        }

        // Check for invalid members and find struct definitions
        foreach(var m in Members) {
            if(m.IsStatic) {
                Error("Shader members should not be static", m);
            }
            if(m is INamedTypeSymbol t) {
                if(!t.IsValueType || !t.IsRecord) {
                    Error("Custom shader types must be record structs");
                }
                List<(string, ValueType)> fields = new();
                foreach(var structMember in t.GetMembers()) {
                    if(structMember is IFieldSymbol f) {
                        if(f.IsStatic) {
                            Error("Shader struct may not contain static members", t);
                        }
                        if(ValidateType(f.Type, f, out var fieldType)) {
                            var name = f.AssociatedSymbol == null ? f.Name : f.AssociatedSymbol.Name;
                            fields.Add((name, fieldType));
                        }
                    }
                }
                var expectedFields = t.Constructors.Max(m => m.Parameters.Length);
                if(fields.Count != expectedFields) {
                    Error("Only simple record structs are permitted in shaders", t);
                }
                Types.CustomStructs.Add(t.ToDisplayString(), new(t.Name, fields.ToImmutableArray()));
            }
        }

        // Find the globals
        var globals = new List<ArgumentInfo>();
        foreach (var m in Members) {
            if (m is IFieldSymbol f) {
                if (ValidateType(f.Type, f, out var r)) {
                    globals.Add(new(f.Name, r, SymbolLoc(f)));
                }
            }
        }
        var orderedGlobals = globals.OrderBy(x => x.Loc.GetLineSpan().StartLinePosition).ToArray();

        MethodInfo? vertex = null;
        MethodInfo? fragment = null;
        List<MethodInfo> helpers = new();

        foreach (var sym in Members) {
            if(sym is IMethodSymbol m) {
                if (sym.Name is "Vertex") GetUniqueMethod(m, ref vertex);
                else if (sym.Name is "Fragment") GetUniqueMethod(m, ref fragment);
                else GetHelperMethod(m, helpers);
            }
        }

        if (vertex is null) Error("Shader requires 'Vertex' method");
        else {
            bool containsPos =
                vertex.Output is Vec4Type ||
                (vertex.Output is CustomStruct cs
                    && cs.Fields.Any(f => f.Item1 == "Pos" && f.Item2 is Vec4Type));
            if(!containsPos) {
                Error("Vertex method must either return Vec4, or a struct with 'Vec4 Pos' field", vertex.Sym);
            }
        }
        if (fragment is null) Error("Shader requires 'Fragment' method");
        else {
            if(fragment.Output is not RGBAType) {
                Error("Fragment method must return RGBA value", fragment.Sym);
            }
        }

        if(Errors.Any())
            return Fail(out result);

        return Success(out result, new(Sym, Syntax, vertex!, fragment!, orderedGlobals, Types));
    }

    public static bool Process(List<Diagnostic> errors, ClassInfo c, out ShaderInfo result) {
        var analyze = new ShaderAnalyze(errors, c);
        return analyze.Process(out result);
    }

}

public class ShaderGenException : Exception {
    public ShaderGenException(string msg) : base(msg) { }
}

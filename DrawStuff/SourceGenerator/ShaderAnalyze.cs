using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections.Immutable;

namespace ShaderCompiler;

public record struct ShaderStructDefinition(
    INamedTypeSymbol Type, StructDeclarationSyntax Syntax, SemanticModel Model);

public record struct ShaderDefinition(
    ITypeSymbol Type, ClassDeclarationSyntax Syntax, SemanticModel Model);

public record struct ArgumentInfo(string Name, TypeTag Type, Location Loc);

public record MethodInfo(IMethodSymbol Sym, ArgumentInfo[] Inputs, TypeTag Output);

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
    ITypeSymbol Sym,
    ClassDeclarationSyntax Syntax,
    MethodInfo Vertex,
    MethodInfo Fragment,
    MethodInfo[] Helpers,
    ArgumentInfo[] Globals,
    ImmutableArray<CustomStruct> CustomStructs);

static class SymbolUtils {
    public static Location GetLoc(ISymbol sym, SyntaxTree localRoot) {
        foreach (var n in sym.DeclaringSyntaxReferences) {
            if (n.SyntaxTree == localRoot)
                return n.GetSyntax().GetLocation();
        }
        foreach (var n in sym.DeclaringSyntaxReferences)
            return n.GetSyntax().GetLocation();
        return localRoot.GetRoot().GetLocation();
    }
}

public class TypeChecker {

    private List<Diagnostic> Errors;
    Dictionary<string, CustomStruct> CustomStructs = new();

    public TypeChecker(List<Diagnostic> diagnostics) {
        Errors = diagnostics;
    }

    private void Error(string message, Location loc) =>
        Errors.Add(Diagnostic.Create(ShaderDiagnostic.InvalidShader, loc, message));

    private void Error(string message, ISymbol sym, SyntaxTree currentTree) =>
        Error(message, SymbolUtils.GetLoc(sym, currentTree));

    public TypeTag? ToBuiltinType(ITypeSymbol sym) => sym.ToDisplayString() switch {
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

    public bool TryGet(ITypeSymbol sym, out TypeTag output) {
        TypeTag? t = ToBuiltinType(sym);
        if (t is null) {
            if (CustomStructs.TryGetValue(sym.ToDisplayString(), out var cs)) {
                t = cs;
            }
        }
        output = t!;
        return t != null;
    }


    public bool ValidateType(ITypeSymbol sym, SyntaxTree tree, out TypeTag r) {
        if (TryGet(sym, out r)) {
            return true;
        }
        else if (ToCustomStruct(sym, tree, out var cs)) {
            r = cs;
            return true;
        }
        Error($"Type {sym.ToDisplayString()} is not supported in shaders", sym, tree);
        return false;
    }

    static string layoutAttribName =
        "System.Runtime.InteropServices.StructLayoutAttribute" +
        "(System.Runtime.InteropServices.LayoutKind.Sequential)";

    public bool ToCustomStruct(ITypeSymbol sym, SyntaxTree tree, out CustomStruct cs) {
        if(sym is not INamedTypeSymbol t) {
            Error("Shader struct invalid", sym, tree);
            cs = default!;
            return false;
        }
        // StructLayout(LayoutKind.Sequential)
        var attribs = sym.GetAttributes();
        bool hasSequentialLayout =
            sym.GetAttributes()
            .Where(a => a.ToString() == layoutAttribName)
            .Any();
        List<(string, TypeTag)> fields = new();
        foreach (var structMember in t.GetMembers()) {
            if (structMember is IFieldSymbol f) {
                if (f.IsStatic) {
                    Error("Shader struct may not contain static members", t, tree);
                }
                if (ValidateType(f.Type, tree, out var fieldType)) {
                    var name = f.AssociatedSymbol == null ? f.Name : f.AssociatedSymbol.Name;
                    fields.Add((name, fieldType));
                }
            }
        }
        var expectedFields = t.Constructors.Max(m => m.Parameters.Length);
        if (fields.Count != expectedFields) {
            Error("Only simple record structs are permitted in shaders", t, tree);
        }
        var loc = SymbolUtils.GetLoc(sym, tree);
        cs = new(t.Name, t.ToString(), hasSequentialLayout, fields.ToImmutableArray(), loc);
        CustomStructs[cs.FullName] = cs;
        return true;
    }
}

public class ShaderAnalyze {

    public ITypeSymbol Sym { get; }
    public ClassDeclarationSyntax Syntax { get; }
    private List<ISymbol> Members = new();
    private List<Diagnostic> Errors;
    private TypeChecker Types;
    private Dictionary<string, CustomStruct> StructsUsed = new();

    public ShaderAnalyze(List<Diagnostic> diagnostics, TypeChecker types, ShaderDefinition c) {
        Sym = c.Type;
        Syntax = c.Syntax;
        Errors = diagnostics;
        Types = types;
        foreach(var m in c.Type.GetMembers()) {
            bool include = m.DeclaringSyntaxReferences.Any(s => s.SyntaxTree == c.Syntax.SyntaxTree);
            if (include) {
                Members.Add(m);
            }
        }
    }

    private Location SymbolLoc(ISymbol sym) =>
        SymbolUtils.GetLoc(sym, Syntax.SyntaxTree);

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

    private bool ValidateType(ITypeSymbol sym, out TypeTag r) {
        bool success = Types.ValidateType(sym, Syntax.SyntaxTree, out r);
        if(success) {
            if(r is CustomStruct cs) {
                StructsUsed[cs.FullName] = cs;
            }
        }
        return success;
    }

    public bool CheckExternalType(TypeTag t) {
        if (t is CustomStruct cs) {
            if (!cs.HasSequentialAttrib) {
                Error($"Shader struct '{cs.Name}' must have the "
                    + "[StructLayout(LayoutKind.Sequential)] attribute, or it "
                    + "cannot be safely serialised to the GPU.",
                    cs.Loc);
                return false;
            }
            foreach (var (_, ft) in cs.Fields) {
                if (!CheckExternalType(ft)) return false;
            }
        }
        return true;
    }

    private bool GetMethod(IMethodSymbol m, out MethodInfo method) {
        var inputs = new List<ArgumentInfo>();
        foreach(var p in m.Parameters) {
            if (ValidateType(p.Type, out var r)) {
                inputs.Add(new(p.Name, r, SymbolLoc(p)));
            }
            if(p.RefKind is not (RefKind.None or RefKind.In)) {
                Error($"Unsupported parameter ref kind {p.RefKind}", p);
            }
        }
        ValidateType(m.ReturnType, out var returnType);
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
            if (m is INamedTypeSymbol t) {
                ValidateType(t, out var _);
            }
        }

        // Find the globals
        var globals = new List<ArgumentInfo>();
        foreach (var m in Members) {
            if (m is IFieldSymbol f) {
                if (ValidateType(f.Type, out var r) && CheckExternalType(r)) {
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
            // Make sure it has a Pos field
            bool containsPos =
                vertex.Output is Vec4Type ||
                (vertex.Output is CustomStruct cs
                    && cs.Fields.Any(f => f.Item1 == "Pos" && f.Item2 is Vec4Type));
            if(!containsPos) {
                Error("Vertex method must either return Vec4, or a struct with 'Vec4 Pos' field", vertex.Sym);
            }
            // Make sure all vertex inputs can be safely passed from C#
            vertex!.Inputs.All(i => CheckExternalType(i.Type));
        }
        if (fragment is null) Error("Shader requires 'Fragment' method");
        else {
            if(fragment.Output is not RGBAType) {
                Error("Fragment method must return RGBA value", fragment.Sym);
            }
        }

        if(Errors.Any())
            return Fail(out result);

        var types = StructsUsed.Values.ToImmutableArray();
        return Success(out result,
            new(Sym, Syntax, vertex!, fragment!, helpers.ToArray(), orderedGlobals, types));
    }

    public static bool ProcessShader(
        List<Diagnostic> errors, TypeChecker types, ShaderDefinition def, out ShaderInfo result)
    {
        var analyze = new ShaderAnalyze(errors, types, def);
        return analyze.Process(out result);
    }

}

public class ShaderGenException : Exception {
    public ShaderGenException(string msg) : base(msg) { }
}

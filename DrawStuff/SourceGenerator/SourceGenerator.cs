using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ShaderCompiler;

#pragma warning disable RS1035 // Do not use banned APIs for analyzers
#pragma warning disable RS2008 // Requires analyzer tracking files for new error codes

[Generator]
public class ShaderGenerator : ISourceGenerator {

    public List<Diagnostic> Errors = new();
    public Dictionary<string, SourceText> ExtensionFiles = new();
    public List<ShaderResult>? ShaderResultLog = null;
    public TypeChecker Types;

    public ShaderGenerator() {
        Types = new(Errors);
    }

    public ShaderGenerator(List<ShaderResult> shaderResultLog) : this() {
        ShaderResultLog = shaderResultLog;
    }

    public void Initialize(GeneratorInitializationContext context) {
        // Register a syntax receiver that will be created for each generation pass
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver(this));
    }

    public void Execute(GeneratorExecutionContext context) {
        try {
            // retrieve the populated receiver 
            if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
                return;

            foreach (var e in Errors) {
                context.ReportDiagnostic(e);
            }
            foreach (var entry in ExtensionFiles) {
                context.AddSource(entry.Key, entry.Value);
            }
        }
        catch (Exception ex) {
            context.ReportDiagnostic(Diagnostic.Create(ShaderDiagnostic.InvalidShader, null, ex.Message));
        }
        
    }

    private void HandleClass(GeneratorSyntaxContext ctx, ClassDeclarationSyntax classDecl) {
        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl) is not ITypeSymbol sym)
            return;
        if (!sym.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "DrawStuff.ShaderProgramAttribute"))
            return;
        var def = new ShaderDefinition(sym, classDecl, ctx.SemanticModel);
        if (ShaderAnalyze.ProcessShader(Errors, Types, def, out var shaderInfo)) {
            var output = EmitSilkGL.GenerateClassExtension(Errors, Types, shaderInfo, ctx.SemanticModel);
            if(ShaderResultLog != null)
                ShaderResultLog.Add(output);
            var filename = $"ShaderGen__{shaderInfo.Sym.Name}.g.cs";
            ExtensionFiles[filename] = SourceText.From(output.CSharpSrc, Encoding.UTF8);
        }
    }

    // Created on demand before each generation pass
    class SyntaxReceiver : ISyntaxContextReceiver {
        ShaderGenerator Gen;

        public SyntaxReceiver(ShaderGenerator gen) {
            Gen = gen;
        }

        public void OnVisitSyntaxNode(GeneratorSyntaxContext ctx) {
            try {
                if (ctx.Node is ClassDeclarationSyntax c)
                    Gen.HandleClass(ctx, c);
            }
            catch (Exception e) {
                var msg = $"Internal ShaderGen exception: {e.Message}";
                Gen.Errors.Add(Diagnostic.Create(ShaderDiagnostic.InvalidShader, null, msg));
            }

        }
    }
}

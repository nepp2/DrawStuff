using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using ShaderCompiler;

#pragma warning disable RS1035 // Do not use banned APIs for analyzers
#pragma warning disable RS2008 // Requires analyzer tracking files for new error codes

[Generator]
public class ShaderGenerator : ISourceGenerator {

    public void Initialize(GeneratorInitializationContext context) {
        // Register a syntax receiver that will be created for each generation pass
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context) {
        try {
            // retrieve the populated receiver 
            if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
                return;

            foreach (var e in receiver.Errors) {
                context.ReportDiagnostic(e);
            }
            foreach (var entry in receiver.ClassExtensionFiles) {
                context.AddSource(entry.Key, entry.Value);
            }
        }
        catch (Exception ex) {
            context.ReportDiagnostic(Diagnostic.Create(ShaderDiagnostic.InvalidShader, null, ex.Message));
        }
        
    }

    // Created on demand before each generation pass
    class SyntaxReceiver : ISyntaxContextReceiver {
        public List<Diagnostic> Errors = new();

        public Dictionary<string, SourceText> ClassExtensionFiles = new();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context) {
            if (context.Node is not ClassDeclarationSyntax c)
                return;
            if (context.SemanticModel.GetDeclaredSymbol(c) is not ITypeSymbol s)
                return;
            if (!s.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "DrawStuff.ShaderProgramAttribute"))
                return;
            try {
                var classInfo = new ClassInfo(s, c, context.SemanticModel);
                if(ShaderAnalyze.Process(Errors, classInfo, out var shaderInfo)) {
                    var output = CodegenCSharp.GenerateClassExtension(Errors, shaderInfo, context.SemanticModel);
                    var filename = $"ShaderGen__{shaderInfo.Sym.Name}.g.cs";
                    ClassExtensionFiles[filename] = SourceText.From(output.CSharpSrc, Encoding.UTF8);
                }
            }
            catch (ShaderGenException e) {
                var msg = $"Internal ShaderGen exception: {e.Message}";
                Errors.Add(Diagnostic.Create(ShaderDiagnostic.InvalidShader, null, msg));
            }
        }
    }
}

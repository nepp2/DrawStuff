
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using DrawStuff;
using ShaderCompiler;
using System.Reflection;

var assembly = typeof(ShaderTests).Assembly;

var results = assembly.GetManifestResourceNames()
    .Select(name => ShaderTests.TestShader(name, ShaderTests.GetBundledSource(assembly, name)));

void OutputCode(string code) {
    Console.WriteLine();
    int lineNum = 0;
    foreach (string line in code.Split('\n')) {
        Console.Write($"{++lineNum,4}>    ");
        Console.WriteLine(line);
    }
    Console.WriteLine();
}

void OutputResult(TestResult r) {
    Console.WriteLine($"Shader test '{r.Name}' {(r.Failed ? "failed:" : "succeeded.")}");
    Console.WriteLine($"Shader test '{r.Name}' CS extension class template:");
    OutputCode(r.Output!.CSharpTemplate.ToString());
    Console.WriteLine($"Shader test '{r.Name}' vertex source:");
    OutputCode(r.Output!.VertexSrc);
    Console.WriteLine($"Shader test '{r.Name}' fragment source:");
    OutputCode(r.Output!.FragmentSrc);
    if(r.Failed) {
        Console.WriteLine($"Shader test '{r.Name}' full source:");
        OutputCode(r.Output!.CSharpSrc);
        Console.WriteLine($"Shader test '{r.Name}' errors:");
        foreach (var error in r.Errors) {
            Console.WriteLine($"   - {error}");
        }
    }
}

foreach (var r in results.Where(r => !r.Failed))
    OutputResult(r);

foreach (var r in results.Where(r => r.Failed))
    OutputResult(r);

record TestResult(string Name) {
    public CodegenOutput? Output = null;
    public List<string> Errors = new();
    public bool Failed { get => Errors.Count > 0; }
}

class ShaderTests {

    public static string GetBundledSource(Assembly assembly, string name) {
        using var resource = assembly.GetManifestResourceStream(name)!;
        using MemoryStream memStrm = new();
        resource.CopyTo(memStrm);
        var bytes = memStrm.ToArray();
        using var mem = new MemoryStream(bytes);
        using StreamReader sr = new StreamReader(mem, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return sr.ReadToEnd();
    }


    public static TestResult TestShader(string shaderName, string inputSrc) {
        var r = new TestResult(shaderName);

        // Parse with the latest C# language version
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        // Get the assembly paths needed to compile simple C# files
        var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = new[] {
            MetadataReference.CreateFromFile(typeof(string).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(typeof(ShaderLang).Assembly.Location),
        };

        // Parse and analyze the input source file
        SyntaxTree inputSyntax = CSharpSyntaxTree.ParseText(inputSrc, parseOptions);
        var compilation = CSharpCompilation.Create(shaderName)
            .AddReferences(references)
            .AddSyntaxTrees(inputSyntax);
        SemanticModel model = compilation.GetSemanticModel(inputSyntax);
        r.Errors.AddRange(model.GetDiagnostics().Select(d => d.ToString()));
        if (r.Failed) return r;

        // Find the shader class
        var visitor = new ClassCollector();
        visitor.Visit(inputSyntax.GetRoot());
        if(visitor.classes.Count != 1) {
            r.Errors.Add($"Expected exactly one class in test source, found {visitor.classes.Count}");
        }
        if(r.Failed) return r;

        // Run the source generator on the input source
        var c = visitor.classes[0];
        if(model.GetDeclaredSymbol(c) is INamedTypeSymbol sym) {
            var diagnostics = new List<Diagnostic>();
            try {
                var classInfo = new ClassInfo { Type = sym, Syntax = c };
                if (ShaderAnalyze.Process(diagnostics, classInfo, out var shaderInfo)) {
                    r.Output = CodegenCSharp.GenerateClassExtension(shaderInfo, model);
                }
            }
            catch (ShaderGenException e) {
                var msg = $"Internal ShaderGen exception: {e.Message}";
                r.Errors.Add(msg);
            }
            r.Errors.AddRange(diagnostics.Select(d => d.ToString()));
        }
        else {
            r.Errors.Add("No type symbol was found for shader class");
        }
        if (r.Failed) return r;

        // Now try compiling the extension class with the input source
        SyntaxTree outputSyntax = CSharpSyntaxTree.ParseText(r.Output!.CSharpSrc, parseOptions);
        var compilationExt = CSharpCompilation.Create($"{shaderName}.ext")
            .AddReferences(references)
            .AddSyntaxTrees(inputSyntax, outputSyntax);
        r.Errors.AddRange(
            compilationExt.GetSemanticModel(outputSyntax)
            .GetDiagnostics().Select(d => d.ToString()));

        return r;
    }
}

class ClassCollector : CSharpSyntaxWalker {
    public List<ClassDeclarationSyntax> classes = new();
    public override void VisitClassDeclaration(ClassDeclarationSyntax node) => classes.Add(node);
}

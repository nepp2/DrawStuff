
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using DrawStuff;
using ShaderCompiler;
using System.Reflection;
using Silk.NET.OpenGL;
using Silk.NET.Maths;

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
    foreach(var sr in r.ShaderResults) {
        Console.WriteLine($"Shader '{sr.Name}' CS extension class template:");
        OutputCode(sr.CSharpTemplate.ToString());
        Console.WriteLine($"Shader '{sr.Name}' vertex source:");
        OutputCode(sr.VertexSrc);
        Console.WriteLine($"Shader '{sr.Name}' fragment source:");
        OutputCode(sr.FragmentSrc);
        if (r.Errors.Any() || r.Warnings.Any()) {
            Console.WriteLine($"Shader '{sr.Name}' full source:");
            OutputCode(sr.CSharpSrc);
        }
    }
    if (r.Warnings.Any()) {
        Console.WriteLine($"Shader test '{r.Name}' warnings:");
        foreach (var error in r.Warnings) {
            Console.WriteLine($"   - {error}");
        }
    }
    if (r.Errors.Any()) {
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
    public List<ShaderResult> ShaderResults = new();
    public List<string> Errors = new();
    public List<string> Warnings = new();
    public bool Failed => Errors.Any();

    public void AddDiagnostics(IEnumerable<Diagnostic> ds) {
        Errors.AddRange(ds.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => $"{d}"));
        Warnings.AddRange(ds.Where(d => d.Severity == DiagnosticSeverity.Warning).Select(d => $"{d}"));
    }
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
            MetadataReference.CreateFromFile(typeof(ShaderLanguage).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(GL).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Matrix4X4<float>).Assembly.Location),
        };

        // Parse and analyze the input source file
        SyntaxTree inputSyntax = CSharpSyntaxTree.ParseText(inputSrc, parseOptions);
        Compilation compilation = CSharpCompilation.Create(shaderName)
            .AddReferences(references)
            .AddSyntaxTrees(inputSyntax);

        var generator = new ShaderGenerator(r.ShaderResults);

        var srcGenDriver = CSharpGeneratorDriver.Create(generator);
        srcGenDriver.RunGeneratorsAndUpdateCompilation(
            compilation, out compilation, out var generatorDiagnostics);
        r.AddDiagnostics(generatorDiagnostics);
        if (r.Failed) return r;

        SemanticModel model = compilation.GetSemanticModel(inputSyntax);
        r.AddDiagnostics(model.GetDiagnostics());
        if (r.Failed) return r;

        // Find the shader class
        //var visitor = new ClassCollector();
        //visitor.Visit(inputSyntax.GetRoot());
        //if(visitor.classes.Count != 1) {
        //    r.Errors.Add($"Expected exactly one class in test source, found {visitor.classes.Count}");
        //}
        //if(r.Failed) return r;

        // Run the source generator on the input source
        //var c = visitor.classes[0];
        //if(model.GetDeclaredSymbol(c) is INamedTypeSymbol sym) {
        //    var diagnostics = new List<Diagnostic>();
        //    try {
        //        var def = new ShaderDefinition { Type = sym, Syntax = c };
        //        if (ShaderAnalyze.ProcessShader(diagnostics, def, out var shaderInfo)) {
        //            r.Output = CodegenGL.GenerateClassExtension(diagnostics, shaderInfo, model);
        //        }
        //    }
        //    catch (ShaderGenException e) {
        //        var msg = $"Internal ShaderGen exception: {e.Message}";
        //        r.Errors.Add(msg);
        //    }
        //    r.AddDiagnostics(diagnostics);
        //}
        //else {
        //    r.Errors.Add("No type symbol was found for shader class");
        //}
        //if (r.Failed) return r;

        //// Now try compiling the extension class with the input source
        //SyntaxTree outputSyntax = CSharpSyntaxTree.ParseText(r.Output!.CSharpSrc, parseOptions);
        //var compilationExt = CSharpCompilation.Create($"{shaderName}.ext")
        //    .AddReferences(references)
        //    .AddSyntaxTrees(inputSyntax, outputSyntax);
        //r.AddDiagnostics(compilationExt.GetSemanticModel(outputSyntax).GetDiagnostics());

        return r;
    }
}

class ClassCollector : CSharpSyntaxWalker {
    public List<ClassDeclarationSyntax> classes = new();
    public List<StructDeclarationSyntax> structs = new();
    public override void VisitClassDeclaration(ClassDeclarationSyntax n) => classes.Add(n);
    public override void VisitStructDeclaration(StructDeclarationSyntax n) => structs.Add(n);
}

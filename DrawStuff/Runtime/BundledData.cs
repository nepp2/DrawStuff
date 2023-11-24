
using System.Reflection;
using System.Text;

namespace DrawStuff;

public static class BundledData {

    public static byte[] GetFile(Assembly assembly, string path) {
        var name = assembly.GetName().Name;
        using var resource = assembly.GetManifestResourceStream($"{name}.{path.Replace('/', '.')}")!;
        using MemoryStream memStrm = new();
        resource.CopyTo(memStrm);
        return memStrm.ToArray();
    }

    public static string GetTextFile(Assembly assembly, string name) {
        var bytes = GetFile(assembly, name);
        using var mem = new MemoryStream(bytes);
        using StreamReader sr = new StreamReader(mem, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return sr.ReadToEnd();
    }

    public static byte[] GetFile(string path) => GetFile(typeof(BundledData).Assembly, path);
    public static string GetTextFile(string path) => GetTextFile(typeof(BundledData).Assembly, path);
}
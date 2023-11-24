
using System.Runtime.CompilerServices;

namespace DrawStuff; 

public static class CompileTime {
    public static string GetCallerPath([CallerFilePath] string path = "") => path;
    public static string GetCallerDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}

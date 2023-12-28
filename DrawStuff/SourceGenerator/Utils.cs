
using System.Collections.Generic;
using System.Linq;

public static class EnumerableExt {
    public static IEnumerable<(T, int)> Indexed<T>(this IEnumerable<T> vs) =>
        vs.Select((v, i) => (v, i));
}

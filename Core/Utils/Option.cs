
using System.Collections;
using System.Runtime.CompilerServices;

namespace DrawStuff;

public record struct FilterEnumerator<A>(IEnumerator<Option<A>> Input) : IEnumerator<A> {
    public A Current { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() {
        while (Input.MoveNext()) {
            if (Input.Current.IsSome(out var v)) {
                Current = v;
                return true;
            }
        }
        return false;
    }

    object IEnumerator.Current => Current!;
    public void Dispose() { }
    public void Reset() {
        Input.Reset();
    }
}

public record struct FilterEnumerable<A>(IEnumerable<Option<A>> Input) : IEnumerable<A> {
    public FilterEnumerator<A> GetEnumerator() => new(Input.GetEnumerator());
    IEnumerator<A> IEnumerable<A>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public record struct FilterEnumerator<A, B>(IEnumerator<A> Input, Func<A, Option<B>> Func) : IEnumerator<B> {
    public B Current { get; private set; }
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() {
        while (Input.MoveNext()) {
            if (Func(Input.Current).IsSome(out var v)) {
                Current = v;
                return true;
            }
        }
        return false;
    }
    object IEnumerator.Current => Current!;
    public void Dispose() { }
    public void Reset() {
        Input.Reset();
    }
}

public record struct FilterEnumerable<A, B>(IEnumerable<A> Input, Func<A, Option<B>> Func) : IEnumerable<B> {
    public FilterEnumerator<A, B> GetEnumerator() => new(Input.GetEnumerator(), Func);
    IEnumerator<B> IEnumerable<B>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class OptionExtensions {
    public static IEnumerable<A> Filter<A>(this IEnumerable<A?> seq) where A : class =>
        seq.Where(x => x != null)!;

    public static IEnumerable<A> Filter<A>(this IEnumerable<A?> seq) where A : struct =>
        seq.Where(x => x != null).Select(x => x!.Value);

    public static FilterEnumerable<A> Filter<A>(this IEnumerable<Option<A>> seq) =>
        new FilterEnumerable<A>(seq);

    public static FilterEnumerable<A, B> Filter<A, B>(this IEnumerable<A> seq, Func<A, Option<B>> filter) =>
        new FilterEnumerable<A, B>(seq, filter);
}

public class Option {
    public static Option None = new();
    private Option() { }

    public static Option<T> Some<T>(T v) => new(v);
}

public struct OptionEnumerator<T> : IEnumerator<T> {
    private bool Complete = false;
    public OptionEnumerator(bool complete, T value) {
        Complete = complete;
        Current = value;
    }
    public T Current { get; private set; }
    public bool MoveNext() {
        if (!Complete) {
            Complete = true;
            return true;
        }
        return false;
    }
    object IEnumerator.Current => Current!;
    public void Dispose() { }
    public void Reset() {
        Complete = false;
    }
}

public record struct Option<T> : IEnumerable<T> {
    private T Value { get; }
    public bool HasValue { get; }

    public Option(T v) {
        Value = v;
        HasValue = true;
    }
    public Option() {
        Value = default!;
        HasValue = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public bool IsSome(out T value) {
        if (HasValue) { value = Value; return true; }
        value = default!;
        return false;
    }

    public static implicit operator Option<T>(T value) => new(value);
    public static implicit operator Option<T>(Option _) => new();

    public OptionEnumerator<T> GetEnumerator() => new(!HasValue, Value);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

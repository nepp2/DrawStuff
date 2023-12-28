using System;

namespace ShaderCompiler;

public struct Result<T, E> {
    public bool Success { get; }
    private T Val;
    private E Error;

    public Result(T value) { Val = value; Success = true; Error = default!; }
    public Result(E error) { Error = error; Success = false; Val = default!; }

    public static implicit operator Result<T, E>(T v) => new(v);
    public static implicit operator Result<T, E>(E e) => new(e);

    public T Value {
        get {
            if (!Success) throw new Exception("Cannot unwrap a failed Result");
            return Val;
        }
    }

    public bool TryValue(out T value) {
        if (Success) value = Val;
        else value = default!;
        return Success;
    }

    public bool SuccessOr(out T value, Action<E> handleError) {
        if (TryValue(out value)) return true;
        handleError(Error);
        return false;
    }
}
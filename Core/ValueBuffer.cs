
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DrawStuff;

public class ValueBuffer<T> : IEnumerable<T> where T : unmanaged {
    public T[] Buffer = new T[1024];
    public int Count = 0;

    public void Clear() {
        Count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public void Push(in T val) {
        if (Count == Buffer.Length) {
            var newBuffer = new T[Buffer.Length * 2];
            Buffer.CopyTo(newBuffer, 0);
            Buffer = newBuffer;
        }
        Buffer[Count] = val;
        Count += 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public ref T Push() {
        if (Count == Buffer.Length) {
            var newBuffer = new T[Buffer.Length * 2];
            Buffer.CopyTo(newBuffer, 0);
            Buffer = newBuffer;
        }
        ref var val = ref Buffer[Count];
        Count += 1;
        return ref val;
    }

    public Span<T> AsSpan() {
        return Buffer.AsSpan()[0..Count];
    }

    public ReadOnlySpan<T> AsReadOnlySpan() {
        return AsSpan();
    }

    public static implicit operator ReadOnlySpan<T>(ValueBuffer<T> v) => v.AsSpan();

    public ReadOnlySpan<U> CastElements<U>() where U : unmanaged
        => MemoryMarshal.Cast<T, U>(AsSpan());

    public IEnumerator<T> GetEnumerator() {
        return Buffer.AsEnumerable().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}

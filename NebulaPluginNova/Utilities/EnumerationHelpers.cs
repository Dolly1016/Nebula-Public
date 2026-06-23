using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Linq.Expressions;

namespace Nebula.Utilities;


public static class EnumerationHelpers
{
    public static System.Collections.Generic.IEnumerable<T> GetFastEnumerator<T>(this Il2CppSystem.Collections.Generic.List<T> list) where T : Il2CppSystem.Object => new Il2CppListEnumerable<T>(list);

    public static System.Collections.Generic.IEnumerable<T> GetFastEnumerator<T>(this Il2CppReferenceArray<T> array) where T : Il2CppSystem.Object => new FastIl2CppReferenceArrayEnumerable<T>(array);
}

public unsafe class Il2CppListEnumerable<T> : System.Collections.Generic.IEnumerable<T>, System.Collections.Generic.IEnumerator<T> where T : Il2CppSystem.Object
{
    private struct Il2CppListStruct
    {
#pragma warning disable CS0169
        private IntPtr _unusedPtr1;
        private IntPtr _unusedPtr2;
#pragma warning restore CS0169

#pragma warning disable CS0649
        public IntPtr _items;
        public int _size;
#pragma warning restore CS0649
    }

    private static readonly int _elemSize;
    private static readonly int _offset;
    private static Func<IntPtr, T> _objFactory = null!;

    static Il2CppListEnumerable()
    {
        _elemSize = IntPtr.Size;
        _offset = 4 * IntPtr.Size;

        var constructor = typeof(T).GetConstructor(new[] { typeof(IntPtr) });
        var ptr = Expression.Parameter(typeof(IntPtr));
        var create = Expression.New(constructor!, ptr);
        var lambda = Expression.Lambda<Func<IntPtr, T>>(create, ptr);
        _objFactory = lambda.Compile();
    }

    private readonly IntPtr _arrayPointer;
    private readonly int _count;
    private int _index = -1;

    public Il2CppListEnumerable(Il2CppSystem.Collections.Generic.List<T> list)
    {
        var listStruct = (Il2CppListStruct*)list.Pointer;
        _count = listStruct->_size;
        _arrayPointer = listStruct->_items;
    }

    object IEnumerator.Current => Current;
    public T Current { get; private set; } = null!;

    public bool MoveNext()
    {
        if (++_index >= _count) return false;
        var refPtr = *(IntPtr*)IntPtr.Add(IntPtr.Add(_arrayPointer, _offset), _index * _elemSize);
        Current = _objFactory(refPtr);
        return true;
    }

    public void Reset()
    {
        _index = -1;
    }

    public System.Collections.Generic.IEnumerator<T> GetEnumerator()
    {
        return this;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this;
    }

    public void Dispose()
    {
    }

}

public unsafe sealed class FastIl2CppReferenceArrayEnumerable<T> :
    IEnumerable<T>,
    IEnumerator<T>
    where T : Il2CppSystem.Object
{
    private static readonly int ArrayDataOffset = 4 * IntPtr.Size;
    private static readonly int ElementSize = IntPtr.Size;

    private static readonly Func<IntPtr, T> ObjectFactory = CreateObjectFactory();

    private readonly IntPtr _arrayPointer;
    private readonly int _length;

    private int _index = -1;

    public FastIl2CppReferenceArrayEnumerable(Il2CppReferenceArray<T> array)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));

        _arrayPointer = array.Pointer;
        _length = array.Length;
    }

    public T Current { get; private set; } = null!;

    object IEnumerator.Current => Current;

    public bool MoveNext()
    {
        int next = _index + 1;

        if (next >= _length)
            return false;

        _index = next;

        IntPtr elementSlot = IntPtr.Add(
            IntPtr.Add(_arrayPointer, ArrayDataOffset),
            next * ElementSize);

        IntPtr objectPointer = *(IntPtr*)elementSlot;

        if (objectPointer == IntPtr.Zero)
        {
            Current = null!;
            return true;
        }

        Current = ObjectFactory(objectPointer);
        return true;
    }

    public void Reset()
    {
        _index = -1;
        Current = null!;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return this;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this;
    }

    public void Dispose()
    {
    }

    private static Func<IntPtr, T> CreateObjectFactory()
    {
        var constructor = typeof(T).GetConstructor(new[] { typeof(IntPtr) });

        if (constructor == null)
            throw new MissingMethodException(
                typeof(T).FullName,
                ".ctor(System.IntPtr)");

        var ptr = Expression.Parameter(typeof(IntPtr), "ptr");
        var create = Expression.New(constructor, ptr);

        return Expression.Lambda<Func<IntPtr, T>>(create, ptr).Compile();
    }
}

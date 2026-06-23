using Il2CppInterop.Runtime.InteropTypes;
using MS.Internal.Xml.XPath;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nebula.Utilities;

public class Il2CppArgument<T>
{
    public T Value { get; private set; }
    public Il2CppArgument(T value)
    {
        Value = value;
    }

    public static implicit operator Il2CppArgument<T>(T value)
    {
        return new Il2CppArgument<T>(value);
    }


}

public static class Il2CppHelpers
{
    private static class CastHelper<T> where T : Il2CppObjectBase
    {
        public static Func<IntPtr, T> Cast;
        static CastHelper()
        {
            var constructor = typeof(T).GetConstructor(new[] { typeof(IntPtr) });
            var ptr = Expression.Parameter(typeof(IntPtr));
            var create = Expression.New(constructor!, ptr);
            var lambda = Expression.Lambda<Func<IntPtr, T>>(create, ptr);
            Cast = lambda.Compile();
        }
    }

    public static T CastFast<T>(this Il2CppObjectBase obj) where T : Il2CppObjectBase
    {
        if (obj is T casted) return casted;
        return obj.Pointer.CastFast<T>();
    }

    public static T CastFast<T>(this IntPtr ptr) where T : Il2CppObjectBase
    {
        return CastHelper<T>.Cast(ptr);
    }

    public static bool TryCast<T>(this Il2CppObjectBase obj,[MaybeNullWhen(false)] out T value) where T : Il2CppObjectBase
    {
        value = obj.TryCast<T>();
        return value != null;
    }


    unsafe public static int GetCurrentPtr()
    {
        var methodPtr = (IntPtr)typeof(UnityEngine.Object).GetField("NativeMethodInfoPtr_op_Implicit_Public_Static_Boolean_Object_0", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        System.IntPtr* ptr = stackalloc System.IntPtr[1]; 
        *ptr = IL2CPP.Il2CppObjectBaseToPtr(null); 
        System.Runtime.CompilerServices.Unsafe.SkipInit(out System.IntPtr exc); 
        System.IntPtr obj = IL2CPP.il2cpp_runtime_invoke(methodPtr, (System.IntPtr)0, (void**)ptr, ref exc); 
        Il2CppInterop.Runtime.Il2CppException.RaiseExceptionIfNecessary(exc); 
        bool val = *(bool*)IL2CPP.il2cpp_object_unbox(obj);
        return (int)obj;
    }
    public static Il2CppSystem.Collections.Generic.List<T> ToIl2CppList<T>(this System.Collections.Generic.IReadOnlyList<T> list)
    {
        Il2CppSystem.Collections.Generic.List<T> result = new(list.Count);
        foreach (var elem in list) result.Add(elem);
        return result;
    }
}


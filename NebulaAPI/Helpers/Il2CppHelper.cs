using Il2CppInterop.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Helpers;

internal static class Il2CppHelper
{
    private static readonly int CachedPtrOffset = (int)IL2CPP.il2cpp_field_get_offset((IntPtr)(typeof(UnityEngine.Object).GetField("NativeFieldInfoPtr_m_CachedPtr", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool AsBoolFast(this UnityEngine.Object? obj)
    {
        if ((object)obj == null) return false;

        if (obj.WasCollected) return false;

        IntPtr objPtr = IL2CPP.Il2CppObjectBaseToPtrNotNull(obj);
        if (objPtr == IntPtr.Zero) return false;

        IntPtr cachedPtr = *(IntPtr*)((byte*)objPtr + CachedPtrOffset);
        return cachedPtr != IntPtr.Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AsBoolFast<T>(this T? obj, out T val) where T : UnityEngine.Object
    {
        val = obj;
        return AsBoolFast(obj);
    }
}

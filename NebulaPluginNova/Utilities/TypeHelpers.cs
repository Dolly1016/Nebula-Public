using Il2CppInterop.Runtime.InteropTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Utilities;

public static class Il2CppFastTypeCheckHelpers
{
    public static bool IsFast<T>(this Il2CppObjectBase obj)
        where T : Il2CppObjectBase
    {
        if (obj == null)
            return false;

        IntPtr targetClass = Il2CppClassPointerStore<T>.NativeClassPtr;
        if (targetClass == IntPtr.Zero)
            return false;

        IntPtr actualClass = IL2CPP.il2cpp_object_get_class(obj.Pointer);

        if (actualClass == targetClass)
            return true;

        return IL2CPP.il2cpp_class_is_assignable_from(targetClass, actualClass);
    }
}

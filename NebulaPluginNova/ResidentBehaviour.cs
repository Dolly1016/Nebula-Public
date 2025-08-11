using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula;

internal class ResidentBehaviour : MonoBehaviour
{
    static ResidentBehaviour()
    {
        ClassInjector.RegisterTypeInIl2Cpp<ResidentBehaviour>();
    }

    void Awake()
    {
        ModSingleton<ResidentBehaviour>.Instance = this;
    }
}

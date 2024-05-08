using Il2CppInterop.Runtime.Injection;

namespace Nebula.Behaviour;

public class EmptyBehaviour : MonoBehaviour
{
    static EmptyBehaviour() => ClassInjector.RegisterTypeInIl2Cpp<EmptyBehaviour>();
}
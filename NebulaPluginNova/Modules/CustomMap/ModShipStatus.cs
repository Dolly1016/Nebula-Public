using Il2CppInterop.Runtime.Injection;

namespace Nebula.Modules.CustomMap;

public class ModShipStatus : ShipStatus
{
    static ModShipStatus() => ClassInjector.RegisterTypeInIl2Cpp<ModShipStatus>();
    public ModShipStatus(System.IntPtr ptr) : base(ptr) { }
    public ModShipStatus() : base(ClassInjector.DerivedConstructorPointer<ModShipStatus>())
    { ClassInjector.DerivedConstructorBody(this); }
}

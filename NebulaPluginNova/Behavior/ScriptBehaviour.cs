using Il2CppInterop.Runtime.Injection;

namespace Nebula.Behavior;

public class ScriptBehaviour : MonoBehaviour
{
    static ScriptBehaviour() => ClassInjector.RegisterTypeInIl2Cpp<ScriptBehaviour>();

    public event Action? UpdateHandler;
    public event Action? ActiveHandler;
    public event Action? DestroyHandler;
    public bool InvokeUpdateOnEnabed { get; set; }
    public void Update()
    {
        UpdateHandler?.Invoke();
    }

    public void OnEnable()
    {
        ActiveHandler?.Invoke();
        if (InvokeUpdateOnEnabed) UpdateHandler?.Invoke();
    }
    public void OnDestroy()
    {
        DestroyHandler?.Invoke();
    }

}

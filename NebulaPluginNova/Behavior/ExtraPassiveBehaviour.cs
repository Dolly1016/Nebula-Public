using Il2CppInterop.Runtime.Injection;

namespace Nebula.Behavior;

public class ExtraPassiveBehaviour : MonoBehaviour
{
    static ExtraPassiveBehaviour() => ClassInjector.RegisterTypeInIl2Cpp<ExtraPassiveBehaviour>();

    private PassiveUiElement myElement = null!;

    public void Start()
    {
        myElement = gameObject.GetComponent<PassiveUiElement>();
    }

    public void Update()
    {
        if(myElement && PassiveButtonManager.Instance.currentOver == myElement)
        {
            OnPiled?.Invoke();
            
            if (Input.GetKeyUp(KeyCode.Mouse1)) OnRightClicked?.Invoke();
        }
    }

    public Action? OnPiled;
    public Action? OnRightClicked;
}

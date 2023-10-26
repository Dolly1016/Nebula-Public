using Il2CppInterop.Runtime.Injection;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace Nebula.Behaviour;

public class ScriptBehaviour : MonoBehaviour
{
    static ScriptBehaviour() => ClassInjector.RegisterTypeInIl2Cpp<ScriptBehaviour>();

    public event Action? UpdateHandler;
    public event Action? ActiveHandler;
    public void Update()
    {
        UpdateHandler?.Invoke();
    }

    public void OnEnable()
    {
        ActiveHandler?.Invoke();
    }
}

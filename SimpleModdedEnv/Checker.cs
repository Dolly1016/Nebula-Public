using BepInEx.Unity.IL2CPP;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using BepInEx.Unity.IL2CPP.Utils.Collections;

namespace DollyProb;

[BepInPlugin("jp.dreamingpig.amongus.dollyProb", "DollyProb", "1.0.0")]
[BepInProcess("Among Us.exe")]
public class Checker : BasePlugin
{
    static public BasePlugin MyPlugin;
    public override void Load()
    {
        MyPlugin = this;
        //new Harmony("jp.dreamingpig.amongus.dollyProb").PatchAll();

        int counter = 0;

        SceneManager.sceneLoaded += (UnityEngine.Events.UnityAction<Scene, LoadSceneMode>)((scene, loadMode) =>
        {
            counter++;
            if(counter >= 3)
            {
                System.Collections.IEnumerator GetEnumerator()
                {
                    yield break;
                }

                Il2CppSystem.Collections.IEnumerator enumerator = GetEnumerator().WrapToIl2Cpp();
                Il2CppSystem.Func<Il2CppSystem.Collections.IEnumerator> func = (System.Func<Il2CppSystem.Collections.IEnumerator>)(() => GetEnumerator().WrapToIl2Cpp());
                
                enumerator.MoveNext();
                Log.LogMessage("[OK!] IEnumerator.MoveNext()");

                func.Invoke().MoveNext();
                Log.LogMessage("[OK!] Func.Invoke().MoveNext()");
                
            }
            
        });
    }
}
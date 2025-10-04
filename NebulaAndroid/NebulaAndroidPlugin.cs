using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Generator.Extensions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using static System.Net.WebRequestMethods;

namespace NebulaAndroid;

[BepInPlugin("jp.dreamingpig.amongus.nebula", "Nebula", "1.0.0")]
[BepInProcess("Among Us.exe")]
public class NebulaLoader : BasePlugin
{
    static public NebulaLoader MyPlugin = null!;

    public static AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
    public static AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
    public static AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");

    public override void Load()
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                LogUtils.WriteToConsole(a.FullName ?? "Null");
            }
            catch (Exception e)
            {

            }
        }
        MyPlugin = this;
        Nebula.NebulaPlugin.LoadForAndroid();
        
    }
}
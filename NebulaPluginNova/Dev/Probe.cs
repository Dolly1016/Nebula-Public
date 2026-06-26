using Epic.OnlineServices.Stats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.LowLevel;
using Virial;

namespace Nebula.Dev;

public class NebulaProfiler
{
#if DUMMY
    public static void InstallDefault(){}
    public static void LapTimer(string tag){}
    public static void LapTimer(string tag, long milliSec){}
    public static void ShowPtrStatus(){}
#else
    private static Cache<TimeLogger> timer = new(() =>
    {
        var logger = NebulaAPI.Logging.CombinedLogger(NebulaAPI.Logging.BepInExLogger(), NebulaAPI.Logging.NebulaLogger());
        return new("Time", text => logger.Message(text));
    });
    public static void ResetTimer() => timer.Get().Restart();
    public static bool LapTimer(string tag, long milliSec = 50) => timer.Get().MeasureIf(tag, milliSec);


    private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();

    private static readonly Dictionary<string, long> StartTicks = new();
    private static readonly Dictionary<string, Stat> Stats = new();

    private static bool _installed;
    private static PlayerLoopSystem _originalLoop;

    private const double SlowThresholdMs = 50.0;
    private const float SummaryIntervalSeconds = 5f;

    private static float _nextSummaryTime;

    public static void InstallDefault()
    {
        InstallUnder(
            Il2CppType.Of<UnityEngine.PlayerLoop.TimeUpdate>(),
            Il2CppType.Of<UnityEngine.PlayerLoop.Initialization>(),
            Il2CppType.Of<UnityEngine.PlayerLoop.EarlyUpdate>(),
            Il2CppType.Of<UnityEngine.PlayerLoop.FixedUpdate>(),
            Il2CppType.Of<UnityEngine.PlayerLoop.PreUpdate>(),
            Il2CppType.Of<UnityEngine.PlayerLoop.Update>(),
            Il2CppType.Of<UnityEngine.PlayerLoop.PreLateUpdate>(),
            Il2CppType.Of<UnityEngine.PlayerLoop.PostLateUpdate>()
        );
    }

    public static void InstallUnder(params Il2CppSystem.Type[] parentTypes)
    {
        if (_installed)
        {
            NebulaAPI.Logging.BepInExLogger().Warning("[DetailedLoopProfiler] already installed");
            return;
        }

        _installed = true;
        _nextSummaryTime = Time.realtimeSinceStartup + SummaryIntervalSeconds;

        var loop = PlayerLoop.GetCurrentPlayerLoop();
        _originalLoop = loop;

        foreach (var parentType in parentTypes)
        {
            bool ok = WrapDirectChildrenOf(ref loop, parentType);
            NebulaAPI.Logging.BepInExLogger().Warning($"[DetailedLoopProfiler] wrap {parentType.FullName}: {ok}");
        }

        PlayerLoop.SetPlayerLoop(loop);
        NebulaAPI.Logging.BepInExLogger().Warning("[DetailedLoopProfiler] installed");
    }

    private static bool WrapDirectChildrenOf(ref PlayerLoopSystem system, Il2CppSystem.Type parentType)
    {
        if (system.type == parentType)
        {
            WrapDirectChildren(ref system);
            return true;
        }

        if (system.subSystemList == null)
            return false;

        bool found = false;

        for (int i = 0; i < system.subSystemList.Length; i++)
        {
            var child = system.subSystemList[i];

            if (WrapDirectChildrenOf(ref child, parentType))
            {
                system.subSystemList[i] = child;
                found = true;
            }
        }

        return found;
    }

    private static void WrapDirectChildren(ref PlayerLoopSystem parent)
    {
        if (parent.subSystemList == null)
            return;

        var parentName = NameOf(parent);
        var list = new List<PlayerLoopSystem>();

        foreach (var child in parent.subSystemList)
        {
            var childName = $"{parentName}/{NameOf(child)}";

            list.Add(MakeProbe("Before", childName));
            list.Add(child);
            list.Add(MakeProbe("After", childName));
        }

        parent.subSystemList = list.ToArray();
    }

    private static PlayerLoopSystem MakeProbe(string kind, string name)
    {
        string capturedName = name;
        string capturedKind = kind;

        return new PlayerLoopSystem
        {
            type = Il2CppType.Of<AmongUsClient>(),
            updateDelegate = (PlayerLoopSystem.UpdateFunction)(() => Probe(capturedKind, capturedName))
        };
    }

    private static void Probe(string kind, string name)
    {
        long now = Stopwatch.ElapsedTicks;

        if (kind == "Before")
        {
            StartTicks[name] = now;
            return;
        }

        if (!StartTicks.TryGetValue(name, out var start))
            return;

        double ms = (now - start) * 1000.0 / Stopwatch.Frequency;

        if (ms >= SlowThresholdMs)
        {
            NebulaAPI.Logging.BepInExLogger().Warning($"[LOOP SLOW] {ms:F2}ms frame={Time.frameCount} {name}");
        }

        if (Time.realtimeSinceStartup >= _nextSummaryTime)
        {
            _nextSummaryTime = Time.realtimeSinceStartup + SummaryIntervalSeconds;
        }
    }

    private static string NameOf(PlayerLoopSystem system)
    {
        return system.type != null ? system.type.FullName : "<null>";
    }

    static private int lastPtr = 0;
    public static void ShowPtrStatus()
    {
        var currentPtr = Il2CppHelpers.GetCurrentPtr();
        LogUtils.WriteToConsole("Ptr Diff:" + (int)((currentPtr - lastPtr) / 16));
        lastPtr = currentPtr;
    }
#endif
}

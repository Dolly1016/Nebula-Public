using Nebula.Behavior;

namespace Nebula.Modules;

[NebulaRPCHolder]
public class ConsoleRestriction
{
    public enum ConsoleType
    {
        Admin = 0,
        Vitals = 1,
        Camera = 2
    }

    private float?[] timer = new float?[3];

    public void Use(ConsoleType type, float used)
    {
        RpcUse.Invoke((type,used));
    }

    public float GetLeftTime(ConsoleType type)=> timer[(int)type] ?? 1000f;
    public bool CanUse(ConsoleType type) => (timer[(int)type] ?? 1000f) > 0f;
    public bool ShouldShowTimer(ConsoleType type) => timer[(int)type].HasValue;

    static private RemoteProcess<(ConsoleType type, float used)> RpcUse = new(
        "UseConsole",
        (message, calledByMe) =>
        {
            var timers = NebulaGameManager.Instance?.ConsoleRestriction.timer;

            if (timers?[(int)message.type] == null) return;
            timers[(int)message.type] -= message.used;
        }
    );

    public void OnMeetingEnd()
    {
        if (!GeneralConfigurations.ResetRestrictionsOption) return;

        OnGameStart();
    }

    public void OnGameStart()
    {
        timer[(int)ConsoleType.Admin] = GeneralConfigurations.AdminRestrictionOption < 0f ? null : GeneralConfigurations.AdminRestrictionOption.GetValue();
        timer[(int)ConsoleType.Vitals] = GeneralConfigurations.VitalsRestrictionOption < 0f ? null : GeneralConfigurations.VitalsRestrictionOption.GetValue();
        timer[(int)ConsoleType.Camera] = GeneralConfigurations.CameraRestrictionOption < 0f ? null : GeneralConfigurations.CameraRestrictionOption.GetValue();
    }

    public ConsoleTimer? ShowTimerIfNecessary(ConsoleRestriction.ConsoleType consoleType,Transform parentTransform,Vector3 localPos)
    {
        if (!ShouldShowTimer(consoleType)) return null;
        return UnityHelper.CreateObject<ConsoleTimer>("Timer", parentTransform, localPos).SetUp(consoleType);
    }
}

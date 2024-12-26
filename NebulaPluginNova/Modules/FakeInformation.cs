using Virial;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Modules;

using FakeAdminParam = (byte playerId, Vector2 position, bool isImpostor, bool isDead);
using FakeVitalsParam = (byte playerId, VitalsState state);

public enum VitalsState
{
    Disconnected,
    Alive,
    Dead,
}

public record FakeAdmin(FakeAdminParam[] Players);
public record FakeVitals(FakeVitalsParam[] Players);

public record FakeInformationEntry<T>(T Information) {
    public float Time;

    public bool Update()
    {
        Time -= UnityEngine.Time.deltaTime;
        return Time < 0f;
    }
}

[NebulaRPCHolder]
internal class FakeInformation : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static public FakeInformation? Instance => NebulaAPI.CurrentGame?.GetModule<FakeInformation>();

    FakeInformationEntry<FakeAdmin>? Admin;
    FakeInformationEntry<FakeVitals>? Vitals;

    public FakeInformation()
    {
        this.Register(Virial.NebulaAPI.CurrentGame!);
    }

    void HudUpdate(GameHudUpdateEvent ev)
    {
        if (Admin?.Update() ?? false) Admin = null;
        if (Vitals?.Update() ?? false) Vitals = null;

    }

    static public RemoteProcess<(FakeAdminParam[] players, float duration)> RpcFakeAdmin = new(
        "FakeAdmin",
        (message, _) =>
        {
            var instance = Instance;
            if (instance != null && (instance?.Admin?.Time ?? 0f) < message.duration) instance!.Admin = new(new(message.players)) { Time = message.duration };
        }
        );

    static public RemoteProcess<(FakeVitalsParam[] players, float duration)> RpcFakeVitals = new(
        "FakeVitals",
        (message, _) =>
        {
            var instance = Instance;
            if (instance != null && (instance?.Vitals?.Time ?? 0f) < message.duration) instance!.Vitals = new(new(message.players)) { Time = message.duration };
        }
        );

    public FakeAdmin CurrentAdmin => Admin?.Information ?? AdminFromActuals;
    public FakeVitals CurrentVitals => Vitals?.Information ?? VitalsFromActuals;

    static public FakeAdmin AdminFromActuals { get
        {
            List<FakeAdminParam> param = new();
            foreach(var d in Helpers.AllDeadBodies()) param.Add(new(d.ParentId, d.TruePosition, false, true));
            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo) if (!p.IsDead) param.Add(new(p.PlayerId, p.VanillaPlayer.GetTruePosition(), p.Role.Role.Category == Virial.Assignable.RoleCategory.ImpostorRole, false));
            return new(param.ToArray());
        } }

    static public FakeVitals VitalsFromActuals
    {
        get
        {
            List<FakeVitalsParam> param = new();
            var deadBodies = Helpers.AllDeadBodies();
            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
            {
                VitalsState state = VitalsState.Alive;
                if(p.IsDead)
                    state = deadBodies.Any(d => d.ParentId == p.PlayerId) ? VitalsState.Dead : VitalsState.Disconnected;

                param.Add(new(p.PlayerId, state));
            }
            return new(param.ToArray());
        }
    }

}

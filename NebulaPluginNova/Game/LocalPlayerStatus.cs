namespace Nebula.Game;

[NebulaRPCHolder]
public class FakeSabotageStatus
{
    HashSet<SystemTypes> fictitiousTasks = new();

    public IEnumerable<SystemTypes> MyFakeTasks => fictitiousTasks;
    public bool HasFakeSabotage(params SystemTypes[] types) => types.Any(fictitiousTasks.Contains);
    private bool PushFakeSabotage(SystemTypes type) => fictitiousTasks.Add(type);
    internal void RemoveFakeSabotage(params SystemTypes[] types) => types.Do(type => fictitiousTasks.Remove(type));
    public void OnMeetingStart()
    {
        fictitiousTasks.Remove(SystemTypes.Reactor);
        fictitiousTasks.Remove(SystemTypes.HeliSabotage);
        fictitiousTasks.Remove(SystemTypes.Laboratory);
        fictitiousTasks.Remove(SystemTypes.LifeSupp);
    }

    public bool OnStartMinigame(PlayerTask task)
    {
        var lastCount = fictitiousTasks.Count;
        fictitiousTasks.RemoveWhere(t=>
        {
            var type = ShipStatus.Instance.GetSabotageTask(t).GetIl2CppType();
            return type.IsEquivalentTo(task.GetIl2CppType());
        });

        return lastCount != fictitiousTasks.Count;
    }

    public static void RpcPushFakeSabotage(GamePlayer player, SystemTypes fakeTask)
    {
        RpcPushTaskToPlayer.Invoke((player.PlayerId, fakeTask));
    }

    public static void RpcRemoveMyFakeSabotage(params SystemTypes[] types)
    {
        using (RPCRouter.CreateSection("PopFakeTask"))
        {
            foreach (var type in types)
            {
                RpcPopTaskToPlayer.Invoke((PlayerControl.LocalPlayer.PlayerId, type));
            }
        }
    }

    static private readonly RemoteProcess<(byte target, SystemTypes task)> RpcPushTaskToPlayer = new(
        "PushFakeTask",
        (message, _) =>
        {

            var player = NebulaGameManager.Instance?.GetPlayer(message.target);
            if (player == null) return;

            if (message.task == SystemTypes.Comms && player.AllAssigned().Any(a => !a.CanFixComm)) return;
            if (message.task == SystemTypes.Electrical && player.AllAssigned().Any(a => !a.CanFixLight)) return;

            player?.Unbox().FakeSabotage.PushFakeSabotage(message.task);

            if (message.target == PlayerControl.LocalPlayer.PlayerId)
            {

                //同様のタスクが無ければ追加
                if(PlayerControl.LocalPlayer.myTasks.Find((Il2CppSystem.Predicate<PlayerTask>)(task=> task.GetIl2CppType().IsEquivalentTo(ShipStatus.Instance.GetSabotageTask(message.task).GetIl2CppType()))) == null)
                {
                    PlayerControl.LocalPlayer.AddSystemTask(message.task);

                    if(message.task == SystemTypes.Reactor || message.task == SystemTypes.Laboratory)
                    {
                        var reactor = ShipStatus.Instance.Systems[message.task].CastFast<ReactorSystemType>();
                        reactor.Countdown = Mathf.Min(reactor.Countdown, reactor.ReactorDuration);
                    }else if (message.task == SystemTypes.LifeSupp)
                    {
                        var reactor = ShipStatus.Instance.Systems[message.task].CastFast<LifeSuppSystemType>();
                        Debug.Log(reactor.Countdown);
                        Debug.Log(reactor.LifeSuppDuration);
                        reactor.Countdown = Mathf.Min(reactor.Countdown, reactor.LifeSuppDuration);
                    }
                    else if (message.task == SystemTypes.HeliSabotage)
                    {
                        var reactor = ShipStatus.Instance.Systems[message.task].CastFast<HeliSabotageSystem>();
                        reactor.Countdown = Mathf.Min(reactor.Countdown, GeneralConfigurations.AirshipHeliDurationOption.CurrentValue);
                        reactor.CompletedConsoles.Clear();
                        reactor.ActiveConsoles.Clear();
                    }
                }
                
            }
        }
        );

    static private readonly RemoteProcess<(byte target, SystemTypes task)> RpcPopTaskToPlayer = new(
        "PopFakeTask",
        (message, _) =>
        {
            NebulaGameManager.Instance?.GetPlayer(message.target)?.Unbox().FakeSabotage.RemoveFakeSabotage(message.task);
        }
        );
}

using AmongUs.GameOptions;
using Virial.DI;
using Virial.Game;

namespace Nebula.Player;

[NebulaRPCHolder]
public class PlayerTaskState : AbstractModule<GamePlayer>, IInjectable, PlayerTasks
{
    //Virial.TasksAPIの実装を兼ねています。
    public int CurrentTasks { get; private set; } = 0;
    public int CurrentCompleted { get; private set; } = 0;
    public int TotalTasks { get; private set; } = 0;
    public int TotalCompleted { get; private set; } = 0;
    public int Quota { get; private set; } = 0;
    public bool IsCrewmateTask { get; private set; } = true;

    AchievementToken<bool>? acTokenGhostTask = null;


    public PlayerTaskState()
    {
        GetTasks(out int shortTasks, out int longTasks, out int commonTasks);
        int sum = shortTasks + longTasks + commonTasks;
        Quota = TotalTasks = CurrentTasks = sum;

        acTokenGhostTask = new("doTaskEvenDead", true, (val, _) => MyContainer.AmOwner && val && Quota >= 8 && TotalCompleted >= Quota);
    }

    public string ToString(bool canSee)
    {
        return canSee ? (TotalCompleted + "/" + Quota) : "?/?";
    }
    public static void GetTasks(out int shortTasks, out int longTasks, out int commonTasks)
    {
        var option = GameOptionsManager.Instance.CurrentGameOptions;
        shortTasks = option.GetInt(Int32OptionNames.NumShortTasks);
        longTasks = option.GetInt(Int32OptionNames.NumLongTasks);
        commonTasks = option.GetInt(Int32OptionNames.NumCommonTasks);
    }

    public void OnCompleteTask()
    {
        RpcUpdateTaskState.Invoke((MyContainer.PlayerId, TaskUpdateMessage.CompleteTask));

        if (acTokenGhostTask != null) acTokenGhostTask.Value &= MyContainer.IsDead;
    }

    public void BecomeToOutsider()
    {
        RpcUpdateTaskState.Invoke((MyContainer.PlayerId, TaskUpdateMessage.BecomeToOutsider));
    }

    public void BecomeToCrewmate()
    {
        RpcUpdateTaskState.Invoke((MyContainer.PlayerId, TaskUpdateMessage.BecomeToCrewmate));
    }

    public void WaiveAndBecomeToCrewmate()
    {
        Quota = 0;
        IsCrewmateTask = true;
        RpcSyncTaskState.Invoke(this);
    }

    public void WaiveAllTasksAsOutsider()
    {
        IsCrewmateTask = false;
        ReplaceTasks(0);
    }

    //指定の個数だけタスクを免除します。
    public void ExemptTasks(int tasks)
    {
        CurrentTasks -= tasks;
        TotalTasks -= tasks;
        Quota -= tasks;
        RpcSyncTaskState.Invoke(this);
    }

    //いま保持しているタスクを新たなものに切り替えます。
    public void ReplaceTasks(int tasks, int unacquired = 0)
    {
        TotalTasks -= CurrentTasks;
        Quota -= CurrentTasks;
        TotalCompleted -= CurrentCompleted;

        CurrentTasks = tasks;
        CurrentCompleted = 0;
        TotalTasks += tasks;
        Quota += tasks + unacquired;
        RpcSyncTaskState.Invoke(this);
    }

    public void GainExtraTasks(int tasks, bool addQuota = false, int unacquired = 0, bool sync = true)
    {
        TotalTasks += tasks;
        CurrentTasks = tasks;
        CurrentCompleted = 0;
        if (addQuota) Quota += tasks;
        if (unacquired > 0) Quota += unacquired;
        if(sync) RpcSyncTaskState.Invoke(this);
    }

    public void RpcSync()
    {
        RpcSyncTaskState.Invoke(this);
    }
    public void ReleaseAllTaskState()
    {
        TotalCompleted = 0;
        TotalTasks = 0;
        CurrentCompleted = 0;
        CurrentTasks = 0;
        Quota = 0;
        RpcSyncTaskState.Invoke(this);
        RecomputeTasks(0, 0, 0);
    }

    private void RemoveAllTasks()
    {
        MyContainer.VanillaPlayer.myTasks.RemoveAll((Il2CppSystem.Predicate<PlayerTask>)((task) => {
            if (ShipStatus.Instance.SpecialTasks.Any(t => t.TaskType == task.TaskType)) return false;
            GameObject.Destroy(task.gameObject);
            return true;
        }));
    }

    public void ResetTasksLocal(List<NetworkedPlayerInfo.TaskInfo> tasks)
    {
        RemoveAllTasks();

        foreach(var t in tasks.Select(task =>
        {
            var orig = ShipStatus.Instance.GetTaskById(task.TypeId);
            var t = GameObject.Instantiate<NormalPlayerTask>(orig, PlayerControl.LocalPlayer.transform);
            t.Id = task.Id;
            t.Index = t.Index;
            t.Owner = PlayerControl.LocalPlayer;
            t.Initialize();
            return t;
        })) MyContainer.VanillaPlayer.myTasks.Add(t);
    }

    private void RecomputeTasks(int shortTasks, int longTasks, int commonTasks)
    {
        RemoveAllTasks();

        Il2CppSystem.Collections.Generic.List<byte> newTaskIdList = new();
        Il2CppSystem.Collections.Generic.List<NormalPlayerTask> taskCandidates = new();
        Il2CppSystem.Collections.Generic.HashSet<TaskTypes> hashSet = new();
        int num = 0;

        num = 0; foreach (var t in ShipStatus.Instance.CommonTasks.ToList().OrderBy(t => Guid.NewGuid())) taskCandidates.Add(t);
        ShipStatus.Instance.AddTasksFromList(ref num, commonTasks, newTaskIdList, hashSet, taskCandidates);
        
        taskCandidates.Clear();

        num = 0; foreach (var t in ShipStatus.Instance.LongTasks.ToList().OrderBy(t => Guid.NewGuid())) taskCandidates.Add(t);
        ShipStatus.Instance.AddTasksFromList(ref num, longTasks, newTaskIdList, hashSet, taskCandidates);

        taskCandidates.Clear();

        num = 0; foreach (var t in ShipStatus.Instance.ShortTasks.ToList().OrderBy(t => Guid.NewGuid())) taskCandidates.Add(t);
        ShipStatus.Instance.AddTasksFromList(ref num, shortTasks, newTaskIdList, hashSet, taskCandidates);

        MyContainer.VanillaPlayer.Data.Tasks = new(newTaskIdList.Count);
        for (int i = 0; i < newTaskIdList.Count; i++)
        {
            MyContainer.VanillaPlayer.Data.Tasks.Add(new NetworkedPlayerInfo.TaskInfo(newTaskIdList[i], (uint)i));
            MyContainer.VanillaPlayer.Data.Tasks[i].Id = (uint)i;
        }

        for (int i = 0; i < MyContainer.VanillaPlayer.Data.Tasks.Count; i++)
        {
            NetworkedPlayerInfo.TaskInfo taskInfo = MyContainer.VanillaPlayer.Data.Tasks[i];
            NormalPlayerTask normalPlayerTask = GameObject.Instantiate<NormalPlayerTask>(ShipStatus.Instance.GetTaskById(taskInfo.TypeId), MyContainer.VanillaPlayer.transform);
            normalPlayerTask.Id = taskInfo.Id;
            normalPlayerTask.Owner = MyContainer.VanillaPlayer;
            normalPlayerTask.Initialize();
            MyContainer.VanillaPlayer.myTasks.Add(normalPlayerTask);
        }
    }

    //実際に保持しているタスクも再計算します
    public void ReplaceTasksAndRecompute(int shortTasks,int longTasks,int commonTasks) {
        RecomputeTasks(shortTasks, longTasks, commonTasks);
        ReplaceTasks(shortTasks + longTasks + commonTasks);
    }

    //実際に保持しているタスクも再計算します
    public void GainExtraTasksAndRecompute(int shortTasks, int longTasks, int commonTasks,bool addQuota = false) {
        RecomputeTasks(shortTasks,longTasks,commonTasks);
        GainExtraTasks(shortTasks+longTasks+commonTasks,addQuota);
    }


    private static RemoteProcess<PlayerTaskState> RpcSyncTaskState = new RemoteProcess<PlayerTaskState>(
        "SyncTaskState",
        (writer, message) =>
        {
            writer.Write(message.MyContainer.PlayerId);
            writer.Write(message.CurrentTasks);
            writer.Write(message.CurrentCompleted);
            writer.Write(message.TotalTasks);
            writer.Write(message.TotalCompleted);
            writer.Write(message.Quota);
            writer.Write(message.IsCrewmateTask);
        },
        (reader) =>
        {
            var task = NebulaGameManager.Instance?.GetPlayer(reader.ReadByte())?.Tasks.Unbox();
            if (task != null)
            {
                task.CurrentTasks = reader.ReadInt32();
                task.CurrentCompleted = reader.ReadInt32();
                task.TotalTasks = reader.ReadInt32();
                task.TotalCompleted = reader.ReadInt32();
                task.Quota = reader.ReadInt32();
                task.IsCrewmateTask = reader.ReadBoolean();
            }
            return task!;
        },
        (message, isCalledByMe) => NebulaGameManager.Instance?.OnTaskUpdated(message.MyContainer)
        );

    private enum TaskUpdateMessage
    {
        CompleteTask,
        BecomeToCrewmate,
        BecomeToOutsider,
        WaiveTasks
    }

    private static RemoteProcess<(byte playerId, TaskUpdateMessage type)> RpcUpdateTaskState = new(
        "UpdateTaskState",
        (message, isCalledByMe) => {
            var player = NebulaGameManager.Instance?.GetPlayer(message.playerId);
            if (player == null) return;
            var task = player!.Tasks.Unbox();
            if (task != null)
            {
                switch (message.type)
                {
                    case TaskUpdateMessage.CompleteTask:
                        task.CurrentCompleted++;
                        task.TotalCompleted++;
                        break;
                    case TaskUpdateMessage.BecomeToCrewmate:
                        task.IsCrewmateTask = true;
                        break;
                    case TaskUpdateMessage.BecomeToOutsider:
                        task.IsCrewmateTask = false;
                        break;
                    case TaskUpdateMessage.WaiveTasks:
                        task.Quota = 0;
                        break;
                }
            }
            NebulaGameManager.Instance?.OnTaskUpdated(player);
        }
        );
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerTasksTrySetLocalEvent : AbstractPlayerEvent
{
    public class Task { 
        internal NetworkedPlayerInfo.TaskInfo MyTask { get; private init; }
        uint Id => MyTask.Id;
        byte TaskTypeId => MyTask.TypeId;
        
        internal Task(NetworkedPlayerInfo.TaskInfo task)
        {
            this.MyTask = task;
        }
    }

    /// <summary>
    /// 追加するタスクです。
    /// このリストは自由に編集することができます。
    /// </summary>
    public List<Task> Tasks { get; private init; }

    public int ExtraQuota { get; private set; } = 0;
    public int AddExtraQuota(int extraQuota) => ExtraQuota += extraQuota;
    internal PlayerTasksTrySetLocalEvent(Virial.Game.Player player, IEnumerable<NetworkedPlayerInfo.TaskInfo> tasks):base(player)
    {
        Tasks = new(tasks.Select(t => new Task(t)));
    }

    internal IEnumerable<NetworkedPlayerInfo.TaskInfo> VanillaTasks => Tasks.Select(t => t.MyTask);
}

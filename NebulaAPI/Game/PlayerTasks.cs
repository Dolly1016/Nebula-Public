using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.DI;

namespace Virial.Game;

public interface PlayerTasks : IModule
{
    /// <summary>
    /// 今所持しているタスクの数です。
    /// </summary>
    int CurrentTasks { get; }
    
    /// <summary>
    /// 今所持しているうちでクリア済みのタスクの数です。
    /// </summary>
    int CurrentCompleted { get; }

    /// <summary>
    /// 対象プレイヤーがこれまでに割り当てられてきた全タスクの数です。
    /// </summary>
    int TotalTasks { get; }

    /// <summary>
    /// 対象プレイヤーがこれまでにクリアしてきたタスクの数です。
    /// </summary>
    int TotalCompleted { get; }

    /// <summary>
    /// 対象プレイヤーに割り当てられたタスクノルマです。
    /// </summary>
    int Quota { get; }

    /// <summary>
    /// タスクがクルーメイトのタスクとして割り当てられている場合Trueが返ります。
    /// </summary>
    bool IsCrewmateTask { get; }

    /// <summary>
    /// 今所持しているタスクをすべてクリアしている場合Trueが返ります。
    /// </summary>

    bool IsCompletedCurrentTasks => CurrentCompleted >= CurrentTasks;

    /// <summary>
    /// 割り当てられてきたタスクすべてをクリアしている場合Trueが返ります。
    /// </summary>
    bool IsCompletedTotalTasks => TotalCompleted >= TotalTasks;

    /// <summary>
    /// タスクノルマをすべてクリアしている場合Trueが返ります。
    /// </summary>
    bool IsAchievedQuota => TotalCompleted >= Quota;

    /// <summary>
    /// 実行可能なタスクを持ちうる場合Trueが返ります。
    /// </summary>
    bool HasExecutableTasks => CurrentTasks > 0;

    /// <summary>
    /// タスクを免除します。ローカルプレイヤーでのみ実行できます。
    /// 免除されたタスクはタスク一覧からは削除されません。余剰のタスクは他人の代わりにこなせます。
    /// </summary>
    /// <returns>免除に成功した場合、true</returns>
    bool ExemptTasks(int numOfTasks);

    /// <summary>
    /// タスクを置き換えます。現在の割り当ては削除されます。ローカルプレイヤーでのみ実行できます。
    /// </summary>
    /// <param name="commonTasks"></param>
    /// <param name="shortTasks"></param>
    /// <param name="longTasks"></param>
    /// <returns></returns>
    bool ReplaceTasks(int commonTasks, int shortTasks, int longTasks);

    /// <summary>
    /// 追加タスクを獲得します。
    /// </summary>
    /// <param name="commonTasks"></param>
    /// <param name="shortTasks"></param>
    /// <param name="longTasks"></param>
    /// <param name="addQuota">ノルマを増加させる場合、true</param>
    /// <returns></returns>
    bool GainExtraTasks(int commonTasks, int shortTasks, int longTasks, bool addQuota);

    /// <summary>
    /// タスクを巻き戻します。ローカルプレイヤーでのみ実行できます。
    /// </summary>
    /// <param name="numOfTasks"></param>
    /// <returns></returns>
    bool RewindTasks(int numOfTasks);


}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Attributes;
using Virial.Game;

namespace Virial.Assignable;

/// <summary>
/// 実行時の役職コンテナの定義の共通部分を表します。
/// RuntimeRoleと紐づけられます。
/// </summary>
public abstract class AbstractRoleInstanceCommon : IBinderLifespan
{
    /// <summary>
    /// 紐づけられた役職コンテナ
    /// </summary>
    public RuntimeRole RuntimeRole { get; internal set; } = null!;

    /// <summary>
    /// 割り当てられたプレイヤ―
    /// </summary>
    public Player MyPlayer => RuntimeRole.MyPlayer;
    
    public bool IsDeadObject => RuntimeRole.IsDeadObject;
    public T Bind<T>(T obj) where T : IReleasable => RuntimeRole.Bind(obj);

    internal abstract void SetRole(AbstractRoleDef roleDef);

    /// <summary>
    /// 役職が割り当てられた際に、クライアントの操作するプレイヤ―についてのみ呼び出されます。
    /// </summary>
    public virtual void OnLocalActivated() { }

    /// <summary>
    /// 役職が割り当てられた際に呼び出されます。
    /// </summary>
    public virtual void OnActivated() { }

    /// <summary>
    /// 常時、クライアントの操作するプレイヤーについてのみ呼び出されます。
    /// </summary>
    public virtual void OnLocalUpdate() { }

    /// <summary>
    /// 常時呼び出されます。
    /// </summary>
    public virtual void OnUpdate() { }

    /// <summary>
    /// ゲーム終了時に勝敗を判定します。ホストによって呼び出されます。
    /// </summary>
    /// <param name="gameEnd">ゲームの終了理由</param>
    /// <returns>勝利している場合はtrue</returns>
    public virtual bool CheckWin(GameEnd gameEnd) { return false; }
}

/// <summary>
/// アドオンによって設計できる実行時の役職コンテナです。
/// RuntimeRoleに紐づけられています。
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class AbstractRoleInstance<T> : AbstractRoleInstanceCommon where T : AbstractRoleDef
{
    /// <summary>
    /// 関連するアドオン役職定義
    /// </summary>
    protected internal T MyRole { get; internal set; } = null!;
    internal override void SetRole(AbstractRoleDef roleDef) => MyRole = (roleDef as T)!;

    /// <summary>
    /// ゲーム終了時に勝敗を判定します。ホストによって呼び出されます。
    /// デフォルトではインポスター勝利とクルー勝利についてのみ考慮しています。
    /// </summary>
    /// <param name="gameEnd">ゲームの終了理由</param>
    /// <returns>勝利している場合はtrue</returns>
    public override bool CheckWin(GameEnd gameEnd)
    {
        if(MyRole.Team == NebulaTeams.CrewmateTeam)
        {
            return gameEnd == NebulaGameEnd.CrewmateGameEnd;
        }else if(MyRole.Team == NebulaTeams.ImpostorTeam)
        {
            return gameEnd == NebulaGameEnd.ImpostorGameEnd;
        }

        return false;
    }
}
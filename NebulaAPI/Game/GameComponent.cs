using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Virial.Game;

/// <summary>
/// ゲームに作用するEntityを表します。
/// </summary>
public interface IGameEntity
{
    /// <summary>
    /// 紐づけられたLifespanの寿命が尽きたときに呼び出されます。この後Entityは削除されます。
    /// </summary>
    public void OnReleased()
    {

    }

    /// <summary>
    /// 毎ティック呼び出されます。
    /// </summary>
    public void Update() { }

    /// <summary>
    /// 毎ティック呼び出されます。
    /// </summary>
    public void HudUpdate() { }

    /// <summary>
    /// ゲーム開始時に呼び出されます。
    /// </summary>
    public void OnGameStart() { }

    /// <summary>
    /// 会議開始時に呼び出されます。
    /// </summary>
    public void OnMeetingStart() { }

    /// <summary>
    /// 追放シーン開始時に呼び出されます。
    /// </summary>
    public void OnStartExileCutScene() { }

    /// <summary>
    /// 会議終了時に呼び出されます。
    /// </summary>
    public void OnMeetingEnd() { }

    /// <summary>
    /// 投票終了時に呼び出されます。
    /// </summary>
    public void OnEndVoting() { }

    /// <summary>
    /// タスクフェイズ再開時に呼び出されます。
    /// </summary>
    public void OnGameReenabled() { }

    /// <summary>
    /// タスク状態が変化した際に呼び出されます。
    /// </summary>
    /// <param name="player"></param>
    public void OnTaskUpdated(Player player) { }

    /// <summary>
    /// 会議が開始する少し前(=レポート発生時)に呼び出されます。
    /// </summary>
    /// <param name="reporter"></param>
    /// <param name="reported"></param>
    public void OnPreMeetingStart(Player reporter, Player? reported) { }

    /// <summary>
    /// 死体通報が発生した際に呼び出されます。OnPreMeetingStartに続いて呼び出されます。
    /// </summary>
    /// <param name="reporter"></param>
    /// <param name="reported"></param>
    public void OnReported(Player reporter, Player reported) { }

    /// <summary>
    /// 緊急会議が発生した際に呼び出されます。OnPreMeetingStartに続いて呼び出されます。
    /// </summary>
    /// <param name="reporter"></param>
    public void OnEmergencyMeeting(Player reporter) { }


    /// <summary>
    /// 何らかの理由でプレイヤーが死亡すると呼び出されます。
    /// </summary>
    /// <param name="dead"></param>
    public void OnPlayerDead(Player dead) { }
    
    /// <summary>
    /// 何らかの理由でプレイヤーが誰かに殺害されると呼び出されます。(OnPlayerDeadLocalはこの直後に呼び出されます。)
    /// </summary>
    /// <param name="dead"></param>
    /// <param name="murderer"></param>
    public void OnPlayerMurdered(Player dead, Player murderer) { }

    /// <summary>
    /// 何らかの理由でプレイヤーが追放されると呼び出されます。(追加追放は対象外)
    /// </summary>
    /// <param name="exiled"></param>
    public void OnPlayerExiled(Player exiled) { }
    
    /// <summary>
    /// 死体が生成されたときに呼び出されます。
    /// </summary>
    /// <param name="deadBody">死体</param>
    internal void OnDeadBodyGenerated(DeadBody deadBody) { }

    /// <summary>
    /// サボタージュマップが開かれるときに呼び出されます。
    /// </summary>
    public void OnOpenSabotageMap() { }

    /// <summary>
    /// 通常のマップが開かれるときに呼び出されます。
    /// </summary>
    public void OnOpenNormalMap() { }

    /// <summary>
    /// アドミンマップを開くときに呼び出されます。
    /// </summary>
    public void OnOpenAdminMap() { }

    /// <summary>
    /// マップが生成されたときに呼び出されます。
    /// </summary>
    public void OnMapInstantiated() { }

    /// <summary>
    /// 自身が票を投じたときに呼び出されます。
    /// ローカルでのみ呼び出されます。
    /// </summary>
    /// <param name="target"></param>
    /// <param name="vote"></param>
    internal void OnCastVoteLocal(byte target, ref int vote) { }

    /// <summary>
    /// 自身の票が投じられたときに呼び出されます。(投票結果開示の瞬間)
    /// ローカルでのみ呼び出されます。
    /// </summary>
    /// <param name="votedFor"></param>
    /// <param name="isExiled"></param>
    internal void OnVotedLocal(PlayerControl? votedFor, bool isExiled) { }

    /// <summary>
    /// 自身に票が投じられたときに呼び出されます。(投票結果開示の瞬間)
    /// ローカルでのみ呼び出されます。
    /// </summary>
    /// <param name="voters"></param>
    internal void OnVotedForMeLocal(PlayerControl[] voters) { }

    /// <summary>
    /// 投票結果開示の瞬間に呼び出されます。 投票結果をここで書き換えることはできません。
    /// ローカルでのみ呼び出されます。
    /// </summary>
    /// <param name="result"></param>
    internal void OnDiscloseVotingLocal(MeetingHud.VoterState[] result) { }

    /// <summary>
    /// ゲームが終了しようとしている際に呼び出されます。優先度が高い理由であれば、乗っ取ることができます。
    /// </summary>
    /// <param name="gameEnd"></param>
    /// <param name="gameEndReason"></param>
    /// <returns></returns>
    public (GameEnd end,GameEndReason reason)? OnCheckGameEnd(GameEnd gameEnd, GameEndReason gameEndReason) { return null; }

    /// <summary>
    /// 役職が割り当てられたときに呼び出されます。
    /// </summary>
    /// <param name="player"></param>
    /// <param name="role"></param>
    public void OnSetRole(Player player, RuntimeRole role) { }
    
    /// <summary>
    /// モディファイアが割り当てられたときに呼び出されます。
    /// </summary>
    /// <param name="player"></param>
    /// <param name="modifier"></param>
    public void OnAddModifier(Player player, RuntimeModifier modifier) { }

    /// <summary>
    /// モディファイアが消去されたときに呼び出されます。
    /// </summary>
    /// <param name="player"></param>
    /// <param name="modifier"></param>
    public void OnRemoveModifier(Player player, RuntimeModifier modifier) { }
}

/// <summary>
/// プレイヤーに紐づけられたEntityを表します。
/// </summary>
public interface IGamePlayerEntity : IGameEntity
{
    /// <summary>
    /// このEntityを所有するプレイヤーを表します。
    /// 所有者は途中で変更しないでください。
    /// </summary>
    public Player MyPlayer { get; }

    /// <summary>
    /// 自身が所有者であることを表します。
    /// </summary>
    public bool AmOwner => MyPlayer.AmOwner;

    /// <summary>
    /// 死亡時に呼び出されます。この直前に、OnExtraExiled,OnExiled,OnMurderedのいずれかが呼び出されます。
    /// </summary>
    public void OnDead() { }

    /// <summary>
    /// 追放時に呼び出されます。
    /// </summary>
    public void OnExtraExiled() { }

    /// <summary>
    /// 追放時に呼び出されます。
    /// </summary>
    public void OnExiled() { }

    /// <summary>
    /// 何者かにキルされたときに呼び出されます。
    /// </summary>
    /// <param name="murder">キラー</param>
    public void OnMurdered(Player murder) { }

    /// <summary>
    /// 自身が誰かをキルしたときに呼び出されます。
    /// </summary>
    /// <param name="target">死亡したプレイヤー</param>
    public void OnKillPlayer(Player target) { }

    /// <summary>
    /// 自身が誰かを追加追放したときに呼び出されます。
    /// </summary>
    /// <param name="target">死亡したプレイヤー</param>
    public void OnExtraExilePlayer(Player target) { }

    /// <summary>
    /// ガードが発生したときに呼び出されます。
    /// </summary>
    /// <param name="killer">キルを試みたプレイヤー</param>
    public void OnGuard(Player killer) { }

    /// <summary>
    /// 自身がタスクを1つ完了するたびに呼び出されます。
    /// ローカルでのみ呼び出されます。
    /// </summary>
    public void OnTaskCompleteLocal() { }

    /// <summary>
    /// 役職が割り当てられたときに呼び出されます。
    /// </summary>
    public void OnSetRole(RuntimeRole role) { }

    /// <summary>
    /// モディファイアが追加されたときに呼び出されます。
    /// </summary>
    /// <param name="modifier"></param>
    public void OnAddModifier(RuntimeModifier modifier) { }

    /// <summary>
    /// モディファイアが消去されたときに呼び出されます。
    /// </summary>
    /// <param name="modifier"></param>
    public void OnRemoveModifier(RuntimeModifier modifier) { }
}

public class GamePlayerEntity : IGameEntity
{
    public Player MyPlayer { get; private init; }

    public GamePlayerEntity(Player player)
    {
        MyPlayer = player;
    }
}

public static class GameEntityExtension
{
    /// <summary>
    /// Entityを現在のゲームに追加します。
    /// </summary>
    /// <typeparam name="Entity"></typeparam>
    /// <param name="gameEntity"></param>
    /// <param name="lifespan"></param>
    /// <returns></returns>
    public static Entity Register<Entity>(this Entity gameEntity, ILifespan lifespan) where Entity : IGameEntity
    {
        NebulaAPI.CurrentGame?.RegisterEntity(gameEntity, lifespan);
        return gameEntity;
    }

    /// <summary>
    /// バインド済みEntityを現在のゲームに追加します。
    /// </summary>
    /// <typeparam name="Entity"></typeparam>
    /// <param name="gameEntity"></param>
    /// <returns></returns>
    public static Entity Register<Entity>(this Entity gameEntity) where Entity : IGameEntity, ILifespan
    {
        NebulaAPI.CurrentGame?.RegisterEntity(gameEntity, gameEntity);
        return gameEntity;
    }
}
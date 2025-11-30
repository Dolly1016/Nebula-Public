using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Compat;
using Virial.DI;
using Virial.Utilities;

namespace Virial.Game;

[Flags]
public enum KillCharacteristics
{
    /// <summary>
    /// 実際のプレイヤーをキルします。
    /// </summary>
    FlagKillRealPlayer      = 0x02,
    /// <summary>
    /// 足元に死体を発生させます。
    /// 死体を残さないキルであればこのフラグは無視されます。
    /// </summary>
    FlagLeftDeadBody = 0x04,
    /// <summary>
    /// 実際のプレイヤーの足元に死体を発生させます。
    /// 死体を残さないキルであればこのフラグは無視されます。
    /// </summary>
    FlagLeftRealDeadBody    = 0x08,

    /// <summary>
    /// キルされそうになると煙になって消滅します。
    /// </summary>
    Disappear = 0,
    /// <summary>
    /// 自身が死体になります。自身が本体でない限り本体は死亡しません。
    /// </summary>
    KillOne = FlagLeftDeadBody,
    /// <summary>
    /// 自身と本体もろとも死亡し、全て死体を発生させます。
    /// 死体を発生させないキルであれば死体は発生しません。
    /// </summary>
    KillAllAndLeaveBodyAll = FlagKillRealPlayer | FlagLeftDeadBody | FlagLeftRealDeadBody,
    /// <summary>
    /// 自身と本体もろとも死亡し、死体は自身の元にのみ残します。
    /// 死体を発生させないキルであれば死体は発生しません。
    /// </summary>
    KillAllAndLeaveBodyOne = FlagKillRealPlayer | FlagLeftDeadBody,
}

/// <summary>
/// プレイヤーのような対象を表します。
/// </summary>
public interface IPlayerlike : IModuleContainer, IGameObject
{
    /// <summary>
    /// プレイヤーのような対象としての一意なIDを返します。
    /// 実際のプレイヤーの場合はプレイヤーIDと一致します。
    /// </summary>
    int PlayerlikeId { get; }

    /// <summary>
    /// 紐づくプレイヤーを表します。
    /// </summary>
    Player RealPlayer { get; }

    /// <summary>
    /// プレイヤーの名前を返します。
    /// </summary>
    string Name { get; }
    string ColoredName { get; }

    /// <summary>
    /// 死亡しているとき、Trueを返します。
    /// </summary>
    bool IsDead { get; }
    bool IsAlive => !IsDead;

    /// <summary>
    /// オブジェクトが有効なとき、Trueを返します。
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// キルの特性を表します。
    /// </summary>
    KillCharacteristics KillCharacteristics { get; }

    /// <summary>
    /// インタラクトの対象になりうるかを返します。
    /// このプロパティはプレイヤーの可視性などを考慮しません。
    /// </summary>
    bool CanBeTarget { get; }

    /// <summary>
    /// プレイヤーの足元の位置を返します。
    /// </summary>
    Virial.Compat.Vector2 TruePosition { get; }

    /// <summary>
    /// 自身が主たる操作主であればtrueを返します。
    /// Playerでない限り、falseを返します。
    /// </summary>
    bool AmController => false;

    /// <summary>
    /// このオブジェクトの管理主であればtrueを返します。
    /// </summary>
    bool AmOwner { get; }

    internal void UpdateVisibility(bool update, bool ignoreShadow, bool showNameText = true);

    internal CosmeticsLayer VanillaCosmetics { get; }

    /// <summary>
    /// プレイヤーの現在の見た目を取得します。
    /// </summary>
    OutfitDefinition CurrentOutfit { get; }

    internal IPlayerLogics Logic { get; }

    /// <summary>
    /// 死が確定しているとき、Trueを返します。
    /// 死亡していても、Trueを返さないときがあります。IsDeadと併用して使用する必要があります。
    /// </summary>
    /// <remarks>
    /// v3.3.0でPlayerからIPlayerlikeに移動。<br />
    /// </remarks>
    bool WillDie => false;

    /// <summary>
    /// ダイブしているとき、Trueを返します。
    /// </summary>
    /// <remarks>
    /// v3.3.0でPlayerからIPlayerlikeに移動。<br />
    /// </remarks>
    bool IsDived => false;

    /// <summary>
    /// テレポート中、Trueを返します。
    /// </summary>
    /// <remarks>
    /// v3.3.0でPlayerからIPlayerlikeに移動。<br />
    /// </remarks>
    bool IsTeleporting => false;

    /// <summary>
    /// 吹き飛ばされているとき、Trueを返します。
    /// </summary>
    /// <remarks>
    /// v3.3.0でPlayerからIPlayerlikeに移動。<br />
    /// </remarks>
    bool IsBlown => false;

    /// <summary>
    /// プレイヤーが不可視なとき、Trueを返します。
    /// ローカルのプレイヤーから見た可視性を表します。
    /// </summary>
    /// <remarks>
    /// v3.3.0でPlayerからIPlayerlikeに移動。<br />
    /// v3.1.0で追加。<br />
    /// </remarks>
    bool IsInvisible => false;

    /// <summary>
    /// 全てのプレイヤーのような対象を取得します。
    /// </summary>
    public static IEnumerable<IPlayerlike> AllPlayerlikes => NebulaAPI.instance.CurrentGame?.GetAllPlayerlikes() ?? [];
    public static IEnumerable<IFakePlayer> AllOwningFakePlayers => AllPlayerlikes.Where(p => p.AmOwner && p is IFakePlayer).Select(p => p as IFakePlayer)!;
    /// <summary>
    /// プレイヤーのような対象を取得します。
    /// </summary>
    /// <param name="playerlikeId">プレイヤーID</param>
    /// <returns></returns>
    public static IPlayerlike? GetPlayerlike(int playerlikeId) => NebulaAPI.instance.CurrentGame?.GetPlayerlike(playerlikeId);

}

internal interface IPlayerLogics
{
    internal UnityEngine.Vector2 Position { get; set; }
    internal UnityEngine.Vector2 TruePosition { get; }
    internal Collider2D GroundCollider { get; }
    internal PlayerAnimations Animations { get; }
    internal Rigidbody2D Body { get; }
    internal IPlayerlike Player { get; }
    internal float TrueSpeed { get; }
    internal void Halt();
    internal void SnapTo(UnityEngine.Vector2 position);
    internal void ClearPositionQueues();
    internal void UpdateNetworkTransformState(bool enabled);
    internal void SetKinematic(bool kinematic) => Body.isKinematic = kinematic;
    internal void SetNormalizedVelocity(UnityEngine.Vector2 direction) => Body.velocity = direction * this.TrueSpeed;
    internal void ResetMoveState();
    internal System.Collections.IEnumerator UseZipline(ZiplineConsole zipline);
    internal System.Collections.IEnumerator UseLadder(Ladder ladder);
    internal System.Collections.IEnumerator UseMovingPlatform(MovingPlatformBehaviour movingPlatform, Variable<bool> done);
    internal void SetMovement(bool canMove);
    internal bool InVent { get; }
    internal bool InMovingPlat => false;
    internal bool OnLadder => false;

    internal bool IsActive => true;
}
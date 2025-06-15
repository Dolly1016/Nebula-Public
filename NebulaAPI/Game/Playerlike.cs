using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Compat;
using Virial.DI;

namespace Virial.Game;

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
    /// 見た目上のプレイヤーを表します。
    /// </summary>
    Player? VisualPlayer { get; }

    /// <summary>
    /// プレイヤーの名前を返します。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 死亡しているとき、Trueを返します。
    /// </summary>
    bool IsDead { get; }

    /// <summary>
    /// キルの被害者たりうるかを返します。
    /// このプロパティの値によらずプレイヤーはキルボタンの対象に入ります。
    /// 被害者たり得ない場合、キルは失敗します。
    /// </summary>
    bool AllowKill { get; }

    /// <summary>
    /// インタラクトの対象になりうるかを返します。
    /// このプロパティはプレイヤーの可視性などを考慮しません。
    /// </summary>
    bool CanBeTarget { get; }

    /// <summary>
    /// プレイヤーの足元の位置を返します。
    /// </summary>
    Vector2 TruePosition { get; }

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

}

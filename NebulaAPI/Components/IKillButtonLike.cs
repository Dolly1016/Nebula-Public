using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Components;

/// <summary>
/// キルボタンに類するボタンを表します。
/// シェリフやジャッカル等のキルボタンが含まれます。
/// </summary>
public interface IKillButtonLike : ILifespan
{
    /// <summary>
    /// 現在のクールダウンを返します。
    /// </summary>
    float Cooldown { get; }
    /// <summary>
    /// クールダウンを設定します。
    /// </summary>
    /// <param name="cooldown"></param>
    void SetCooldown(float cooldown);
    /// <summary>
    /// クールダウンをリセットします。
    /// </summary>
    /// <param name="ratio">クールダウンに乗じる値。1の場合通常通りのクールダウンが開始する。</param>
    void StartCooldown(float ratio = 1f);
}

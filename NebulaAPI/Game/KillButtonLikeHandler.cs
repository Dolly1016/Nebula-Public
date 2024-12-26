using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Components;
using Virial.Events.Player;

namespace Virial.Game;

/// <summary>
/// キルボタンに類する対象を管理します。
/// </summary>
public interface KillButtonLikeHandler
{
    /// <summary>
    /// ゲーム中のキルボタンに類するボタンを取得します。
    /// </summary>
    internal IEnumerable<IKillButtonLike> KillButtonLike { get; }
    /// <summary>
    /// キルボタンに類するボタンを登録します。
    /// </summary>
    /// <param name="killButtonLike"></param>
    void Register(IKillButtonLike killButtonLike);

    /// <summary>
    /// キルクールダウンを開始します。このとき、キルクールダウンを最終的に決定するイベントを発火させます。
    /// </summary>
    void StartCooldown();
    /// <summary>
    /// キルクールダウンを変更します。イベントは発火しません。
    /// </summary>
    void SetCooldown(float cooldown);
}

/// <summary>
/// キルボタンに類する対象を管理する単純な実装。
/// 実際にはこの実装が使われていない可能性があります。
/// </summary>
internal class KillButtonLikeHandlerImpl : KillButtonLikeHandler
{
    List<IKillButtonLike> killButtonLikes = new List<IKillButtonLike>();

    IEnumerable<IKillButtonLike> KillButtonLikeHandler.KillButtonLike => killButtonLikes.Where(kbl => kbl.IsAliveObject);

    void KillButtonLikeHandler.Register(IKillButtonLike killButtonLike)
    {
        killButtonLikes.RemoveAll(kbl => kbl.IsDeadObject);
        killButtonLikes.Add(killButtonLike);
    }

    void KillButtonLikeHandler.StartCooldown()
    {
        var ev = NebulaAPI.RunEvent(new ResetKillCooldownLocalEvent(NebulaAPI.CurrentGame!.LocalPlayer));
        killButtonLikes.Do(kbl =>
        {
            if (ev.UseDefaultCooldown)
                kbl.StartCooldown();
            else
                kbl.SetCooldown(ev.FixedCooldown!.Value);
        });
    }
    void KillButtonLikeHandler.SetCooldown(float cooldown)
    {
        killButtonLikes.Do(kbl =>
        {
            kbl.SetCooldown(cooldown);
        });
    }

    public KillButtonLikeHandlerImpl(IKillButtonLike vanillaKillButton)
    {
        killButtonLikes.Add(vanillaKillButton);
    }
}
